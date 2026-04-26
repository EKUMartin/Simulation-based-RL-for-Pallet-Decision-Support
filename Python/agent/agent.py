import torch
import torch.nn as nn
import torch.optim as optim
import numpy as np
import torch.nn.functional as F
from torch.distributions import Categorical

class PPOAgent:
    def __init__(self,model,lr,gamma,gae_lambda,eps_clip, k_epochs, ent_coef=0.01, device='cpu', vf_coef=0.5, batch_size=128):
        self.model = model.to(device)
        self.optimizer = optim.Adam(self.model.parameters(), lr=lr)
        self.gamma = gamma
        self.gae_lambda = gae_lambda
        self.eps_clip = eps_clip
        self.k_epochs = k_epochs
        self.ent_coef = ent_coef
        self.device = device
        self.mse_loss = nn.MSELoss()
        self.vf_coef = vf_coef
        self.batch_size = batch_size 
   
    def select_action(self,data,feasibility_map,width,length):
        data=data.to(self.device)
        feasibility_map=feasibility_map.to(self.device)
        logits,value=self.model(data,feasibility_map)
        dist=Categorical(logits=logits)
        action=dist.sample()
        log_prob=dist.log_prob(action)
        index=action.item()
        area=width*length
        rotation=False
        
        if index>=area:#index 기준 area보다 뒤에 존재하면 90도 rotate
            index=index-area
            rotation=True
        
        x_coordinates=index % width
        y_coordinates=index // width
        
        return x_coordinates, y_coordinates, rotation, action, log_prob, value
    
    def update(self,memory,next_value=0):
        # 데이터
        rewards=torch.tensor(memory.rewards).to(self.device)
        old_log_probs = torch.tensor(memory.log_probs, dtype=torch.float32).to(self.device)
        old_values = torch.tensor(memory.values, dtype=torch.float32).to(self.device)
        dones = torch.tensor(memory.is_terminals, dtype=torch.float32).to(self.device)
        
        states_tensor=torch.stack(memory.states).to(self.device)
        actions_tensor=torch.tensor(memory.actions).to(self.device)
        fm_tensor=torch.stack(memory.feasibility_maps).to(self.device)
        
        # GAE 적용
        advantages=[]
        gae=0
        for t in reversed(range(len(rewards))):
            if t==len(rewards)-1:
                next_val=next_value
            else:
                next_val=old_values[t+1]
            delta=rewards[t]+self.gamma*next_val*(1-dones[t])-old_values[t]
            gae=delta+self.gamma*self.gae_lambda*(1-dones[t])*gae
            advantages.insert(0,gae)
        advantages=torch.tensor(advantages,dtype=torch.float32).to(self.device)
        returns=advantages+old_values
        advantages = (advantages - advantages.mean()) / (advantages.std() + 1e-7)
        #학습
        data_len=len(memory.states)
        batch_size=self.batch_size
        idx=np.arange(data_len)

        for e in range(self.k_epochs):
            np.random.shuffle(idx)

            for i in range(0,data_len,batch_size):
                end_idx=min(i+batch_size,data_len)
                batch_idx=idx[i:end_idx]
                self.optimizer.zero_grad()

                state=states_tensor[batch_idx]
                action=actions_tensor[batch_idx]
                old_log_prob=old_log_probs[batch_idx]
                advantage=advantages[batch_idx]
                ret=returns[batch_idx]
                old_val=old_values[batch_idx]
                fm=fm_tensor[batch_idx]

                logits,values=self.model(state,fm)
                dist=Categorical(logits=logits)
                new_log_prob=dist.log_prob(action)
                dist_entropy=dist.entropy().mean()

                ratio=torch.exp(new_log_prob-old_log_prob)
                surr1=ratio*advantage
                surr2=torch.clamp(ratio,1-self.eps_clip,1+self.eps_clip)*advantage
                pg_loss=-torch.min(surr1,surr2).mean()
                
                values=values.squeeze()
                value_pred_clipped=old_val+(values-old_val).clamp(-self.eps_clip,self.eps_clip)
                v_loss1=self.mse_loss(values,ret)
                v_loss2=self.mse_loss(value_pred_clipped,ret)
                v_loss=0.5*torch.max(v_loss1,v_loss2).mean()

                ent=-dist_entropy

                loss=pg_loss+self.vf_coef*v_loss+self.ent_coef*ent
                loss.backward()

                torch.nn.utils.clip_grad_norm_(self.model.parameters(), max_norm=0.5)
                self.optimizer.step()
        return pg_loss.item(), v_loss.item()





class Memory:
    def __init__(self):
        self.actions=[]
        self.states=[]
        self.log_probs=[]
        self.rewards=[]
        self.feasibility_maps=[]
        self.is_terminals=[]
        self.values=[]
    
    def clear(self):
        del self.actions[:]
        del self.states[:]
        del self.log_probs[:]
        del self.rewards[:]
        del self.feasibility_maps[:]
        del self.is_terminals[:]
        del self.values[:]
