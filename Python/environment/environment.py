from pathlib import Path
import sys
BASE = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(BASE))
from connect_unity.connect_unity import connect_unity as cu

import numpy as np


class Environment:
    def __init__(self,penalty_threshold):
       self.shelf=[0,0]
       self.current_state=[]
       self.boxes=[]
       self.current_box=0
       self.current_feasibility_map=[]
       self.action_history = []
       self.packed_boxes = []
       self.history_boxes=[]
       self.done=False
       self.penalty_threshold=penalty_threshold
       self.cumulated_step=0
    
    def step(self,action):
        """
        state받고->박스 위치 구하고->state update
        박스 개수가 끝날 때까지
        """
        x, y, rotation = action
        box = self.boxes[self.current_box]
        box_w, box_h = box[0], box[1]
        if rotation:
            box_w, box_h = box_h, box_w
        state = np.array(self.current_state)
        grid_height, grid_width = state.shape
        is_invalid = False
        if x + box_w > grid_width or y + box_h > grid_height:
            is_invalid = True
        elif np.sum(state[y:y+box_h, x:x+box_w]) > 0:
            is_invalid = True
        if is_invalid:
            penalty=0
            self.done = True
            print("####################겹침 실패!####################")
            penalty=-1.0+self.cumulated_step
            return self.current_state, self.current_feasibility_map, penalty, self.done
        self.update_state(action)
        self.packed_boxes.append(self.boxes[self.current_box])
        self.action_history.append(action)
        total_space=grid_height*grid_width*4
        # step_reward = (box[0] * box[1]) * 0.0001#박스를 뒀을 때 보상 추가
        step_reward=(box[0] * box[1])/total_space
        self.cumulated_step+=step_reward
        self.current_box+=1
        # step_reward=0

        if self.current_box >= len(self.boxes):
            cu.execute_step(self.action_history)
            # batch_reward = self.get_stepwise_reward()
            # step_reward += batch_reward
            self.shelf, self.current_state, self.boxes = cu.get_observation()
            self.current_box = 0
            self.action_history = []
            if len(self.boxes) == 0:
                terminal_reward = self.get_terminal_reward()
                self.done = True
                # return  self.current_state, self.current_feasibility_map, step_reward + terminal_reward, self.done
                return self.current_state, self.current_feasibility_map, terminal_reward, self.done
        next_box = self.boxes[self.current_box]
        self.current_feasibility_map = self.get_feasibility_map(self.current_state, next_box)
        self.is_done(self.current_feasibility_map,self.boxes)
        
        if self.done:
            terminal_reward=self.get_terminal_reward()
            # return self.current_state, self.current_feasibility_map, step_reward + terminal_reward, self.done
            return self.current_state, self.current_feasibility_map, terminal_reward, self.done    
        
        return self.current_state, self.current_feasibility_map, step_reward, self.done
    def reset(self):
        cu.reset()
        self.shelf, self.current_state, self.boxes = cu.get_observation()
        self.cumulated_step=0
        self.current_box = 0
        self.done = False
        self.packed_boxes = []

        box = self.boxes[self.current_box]
        self.current_feasibility_map = self.get_feasibility_map(self.current_state, box)
        
        return self.current_state, self.current_feasibility_map
    def get_state(self):
        """
        유니티 연결 함수에서 받고
        step에서 사용할 feasibility map과 상자 분리
        """
        box=self.boxes[self.current_box]
        self.current_feasibility_map=self.get_feasibility_map(self.current_state,self.current_box)
        
        return box,self.current_feasibility_map



    
    def get_terminal_reward(self):
        """
        에피소드가 종료 됐을 때 보상 또는 페널티 제공
        unity 연결 함수에서 받음
        """
        # _,information,boxes=cu.get_observation()
        space=np.array(self.current_state)
        total_boxes=len(self.boxes)
        space_left=(space==0).sum()
        space_utilized=(space==1).sum()
        total_space = space.size*4
        total_size=0
        if self.done:
            # print("####################실패!####################")
            # for i in range(total_boxes-self.current_box):
            #     width=self.boxes[self.current_box+i][0]
            #     length=self.boxes[self.current_box+i][1]
            #     total_size+=width*length
            # penalty=space_left-total_size
            # return -penalty/total_space
            return -1
        
        else:
            # time=cu.get_result_episode()
            # weight=np.array([box[2] for box in self.packed_boxes])
            # time_array=np.array([t[0] for t in time])
            # total_time=np.sum(time_array)+1e-9
            # avg_weight=np.average(weight)+1e-9
            # reward=(space_utilized-space_left)/(total_time/avg_weight)
            # reward=space_utilized
            # if reward/total_space>=0.85:
            #     return total_space/reward
            # else:
            #     return reward/total_space
            return 0.0
            # else:
            #     return reward/total_space-0.6

    
    def get_stepwise_reward(self):
        """
        모든 상자가 다 끝났을 때 다음 observation이 오기전에 reward 계산
        """
        # step_result=cu.get_result()
        # performance=0
        # worst_case=0
        # distances=[]
        # weights=[]
        # for i,j in zip(step_result,self.boxes):
        #     distance,weight=i[3],j[2]
        #     distances.append(distance)
        #     weights.append(weight)
        #     performance+=weight/(distance+1e-9)
        # sorted_distance=np.sort(distances)
        # sorted_weight=np.sort(weights)
        # for w,d in zip(sorted_weight,sorted_distance):
        #     worst_case+=w/(d+1e-9)
        # diff=performance-worst_case
        # penalty=self.penalty_threshold*worst_case
        # if diff>=penalty:
        #     return diff*0.5
        # else:
        #     return -penalty*0.5



    def get_feasibility_map(self,current_state,box):
        """
        get_state에서 사용할 feasibility map 계산하는 함수
        """
        box_w=box[0]
        box_h=box[1]
        state=np.array(current_state)
        grid_height,grid_width=state.shape
        f_map_norotate = np.ones_like(state)
        f_map_rotate = np.ones_like(state)
        #원본
        for y in range(grid_height - box_h + 1):
            for x in range(grid_width - box_w + 1):
                if np.sum(state[y : y + box_h, x : x + box_w]) == 0:
                    f_map_norotate[y, x] = 0
        
        #90도회전
        for y in range(grid_height - box_w + 1):
            for x in range(grid_width - box_h + 1):
                if np.sum(state[y : y + box_w, x : x + box_h]) == 0:
                    f_map_rotate[y, x] = 0
        feasibility_map = np.concatenate((f_map_norotate.flatten(), f_map_rotate.flatten()))
        
        return feasibility_map

    def is_done(self,feasibility_map,boxes):
        """
        상자가 남았는데 못넣는지 확인
        상자가 남았는지랑
        현재 넣을 수 있는 공간이 있는지 확인
        """
        if 0 in feasibility_map:
            pass
        else:
            if (len(boxes)-self.current_box)>0:
                self.done=True

    def update_state(self,action):
        """
        박스 하나 넣었을 때마다 업데이트하는 용 feasibility map도 같이 업데이트
        """
        x,y,rotation=action
        box=self.boxes[self.current_box]
        box_w=box[0]
        box_h=box[1]

        if  rotation:
            box_w,box_h=box_h,box_w
        state=np.array(self.current_state)
        state[y:y+box_h,x:x+box_w]=1
        self.current_state=state
    def get_remaining_stats(self):
        remaining_boxes = self.boxes[self.current_box+1:] 
        if not remaining_boxes:
            return np.zeros(6)

        count = len(remaining_boxes)
        widths = [b[0] for b in remaining_boxes]
        lengths = [b[1] for b in remaining_boxes]
        # weights = [b[2] for b in remaining_boxes]
        total_area = sum([w * l for w, l in zip(widths, lengths)])
        
        # 통계치 계산 (정규화 포함)
        avg_w = np.mean(widths) / self.shelf[0]
        avg_l = np.mean(lengths) / self.shelf[1]
        max_w = np.max(widths) / self.shelf[0]
        max_l = np.max(lengths) / self.shelf[1]
        total_area_ratio = total_area / (self.shelf[0] * self.shelf[1])
        rem_count_ratio = count /len(self.boxes)

        return np.array([avg_w, avg_l, max_w, max_l, total_area_ratio, rem_count_ratio])
