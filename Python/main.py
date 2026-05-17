from policy.policy import ActorCritic
from agent.agent import PPOAgent, Memory
from connect_unity.connect_unity import connect_unity
from environment.environment import Environment
import torch
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import os
import csv  # CSV 저장을 위한 모듈 추가

def train():
    device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
    print(f"학습 진행 장치: {device}")
    
    env = Environment(0.4)
    current_state, feasibility_map = env.reset()
    SAVE_DIR = "./checkpoints"
    os.makedirs(SAVE_DIR, exist_ok=True)
    TARGET_REWARD = 0.85
    shelf_width = env.shelf[0]
    shelf_length = env.shelf[1]
    grid_area = shelf_width * shelf_length
    output_size = grid_area * 2
    
    model = ActorCritic(grid_h=shelf_length, grid_w=shelf_width, output_size=output_size).to(device)
    
    lr = 1e-4
    gamma = 0.99
    gae_lambda = 0.95
    eps_clip = 0.2
    k_epochs = 4
    batch_size = 64
    ent_coef = 1e-4
    agent = PPOAgent(model, lr, gamma, gae_lambda, eps_clip, k_epochs, ent_coef, device=device, batch_size=batch_size)
    memory = Memory()
    
    max_episodes = 30000
    update_timestep = 128
    timestep = 0
    reward_history = []
    
    # 🌟 CSV 파일 초기화 및 헤더 작성
    csv_path = "training_logs.csv"
    with open(csv_path, mode='w', newline='', encoding='utf-8-sig') as f:
        writer = csv.writer(f)
        writer.writerow(["Episode", "Total_Timesteps", "Episode_Steps", "Reward", "PG_Loss", "V_Loss"])
    
    for episode in range(1, max_episodes + 1):
        current_state, feasibility_map = env.reset()
        episode_reward = 0
        episode_steps = 0
        
        # 🌟 에피소드별 Loss 기록용 리스트
        ep_pg_losses = []
        ep_v_losses = []
        
        while not env.done:
            timestep += 1
            episode_steps += 1
            box = env.boxes[env.current_box]
            rem_stats = env.get_remaining_stats()
            norm_box_info = np.array([box[0]/shelf_width, box[1]/shelf_length])
            combined_box_data = np.concatenate((norm_box_info, rem_stats))

            flat_state = np.array(env.current_state).flatten()
            flat_fm = np.array(feasibility_map).flatten()
            combined_state = np.concatenate((flat_state, flat_fm, combined_box_data))
            
            state_tensor = torch.FloatTensor(combined_state).to(device)
            fm_tensor = torch.FloatTensor(flat_fm).to(device)
            
            x, y, rotation, action_idx, log_prob, value = agent.select_action(
                state_tensor, fm_tensor, shelf_width, shelf_length
            )
            
            action_tuple = (x, y, rotation)
            
            next_state, next_feasibility_map, reward, done = env.step(action_tuple)
            
            memory.states.append(state_tensor)
            memory.feasibility_maps.append(fm_tensor)
            memory.actions.append(action_idx.item() if torch.is_tensor(action_idx) else action_idx)
            memory.log_probs.append(log_prob.item())
            memory.values.append(value.item())
            memory.rewards.append(reward)
            memory.is_terminals.append(done)
            
            current_state = next_state
            feasibility_map = next_feasibility_map
            episode_reward += reward
            
            if timestep % update_timestep == 0:
                with torch.no_grad():
                    if done:
                        next_value = 0.0
                    else:
                        next_box = env.boxes[env.current_box]
                        n_flat_state = np.array(current_state).flatten()
                        n_flat_fm = np.array(feasibility_map).flatten()
                        norm_n_box_w = next_box[0] / shelf_width
                        norm_n_box_h = next_box[1] / shelf_length
                        norm_n_box_info = np.array([norm_n_box_w, norm_n_box_h])
                        n_rem_stats = env.get_remaining_stats()
                        
                        n_combined_box_data = np.concatenate((norm_n_box_info, n_rem_stats))
                        n_combined = np.concatenate((n_flat_state, n_flat_fm, n_combined_box_data))
                        
                        n_state_tensor = torch.FloatTensor(n_combined).to(device)
                        n_fm_tensor = torch.FloatTensor(n_flat_fm).to(device)
                        
                        _, next_val_tensor = model(n_state_tensor, n_fm_tensor)
                        next_value = next_val_tensor.item()
                        
                pg_loss, v_loss = agent.update(memory, next_value=next_value)
                
                # 🌟 업데이트 발생 시 Loss 저장
                ep_pg_losses.append(pg_loss)
                ep_v_losses.append(v_loss)
                
                memory.clear()
                print(f"  └── [Update] Timestep: {timestep} | 에피소드: {episode} | PG Loss: {pg_loss:.4f} | Value Loss: {v_loss:.4f}")
                
        reward_history.append(episode_reward)
        print(f"Episode {episode}/{max_episodes} 완료 | 총 보상: {episode_reward:.2f}")
        
        # 🌟 에피소드 평균 Loss 계산 (해당 에피소드에서 업데이트가 없었을 경우 0으로 처리)
        avg_pg_loss = sum(ep_pg_losses) / len(ep_pg_losses) if ep_pg_losses else 0.0
        avg_v_loss = sum(ep_v_losses) / len(ep_v_losses) if ep_v_losses else 0.0
        
        # 🌟 매 에피소드 종료 직후 CSV 파일에 실시간 데이터 추가 (Append 모드)
        with open(csv_path, mode='a', newline='', encoding='utf-8-sig') as f:
            writer = csv.writer(f)
            writer.writerow([episode, timestep, episode_steps, episode_reward, avg_pg_loss, avg_v_loss])
            
        # 🌟 타겟 보상 달성 시 모델 저장
        if episode_reward >= TARGET_REWARD:
            save_path = os.path.join(SAVE_DIR, f"ppo_model_ep{episode}_reward{episode_reward:.2f}.pth")
            torch.save(agent.model.state_dict(), save_path)
            print(f"🎉 목표 보상 달성! 모델 저장 완료: {save_path}")
            
        # 🌟 1000 에피소드마다 정기 체크포인트 모델 저장
        if episode % 1000 == 0:
            chkpt_path = os.path.join(SAVE_DIR, f"ppo_model_checkpoint_ep{episode}.pth")
            torch.save(agent.model.state_dict(), chkpt_path)
            print(f"💾 1000 에피소드 정기 체크포인트 저장 완료: {chkpt_path}")

    # 학습 종료 후 그래프 출력
    window_size = 10
    moving_avg = pd.Series(reward_history).rolling(window=window_size, min_periods=1).mean()

    plt.figure(figsize=(10, 6))
    
    plt.plot(reward_history, label='Episode Reward', alpha=0.3, color='steelblue')
    plt.plot(moving_avg, label=f'{window_size}-Episode Moving Avg', color='red', linewidth=2)

    plt.title("PPO Agent Training Rewards")
    plt.xlabel("Episode")
    plt.ylabel("Reward")
    plt.legend()
    plt.grid(True, alpha=0.3)
    plt.show()

if __name__ == "__main__":
    train()