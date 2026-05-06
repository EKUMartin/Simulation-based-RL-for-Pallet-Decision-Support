from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.base_env import ActionTuple
import numpy as np

class connect_unity:
    env = None
    behavior_name = None
    
    shelf_w = 11
    shelf_h = 152
    
    last_weight = 0.0
    current_weight = 0.0
    
    # 🌟 [핵심] 매니저가 결정한 층수(0 또는 1)를 기억해둘 클래스 변수
    current_floor_offset = 0 

    @classmethod
    def reset(cls):
        """에피소드 초기화"""
        if cls.env is None:
            cls.env = UnityEnvironment(file_name=None, seed=1, side_channels=[])
            cls.env.reset()
            cls.behavior_name = list(cls.env.behavior_specs.keys())[0]
        else:
            cls.env.reset()
            cls.last_weight = 0.0

    @classmethod
    def get_observation(cls):
        """유니티에서 데이터를 가져와서 현재 배치할 층의 지도로 복원"""
        decision_steps, terminal_steps = cls.env.get_steps(cls.behavior_name)

        if len(terminal_steps) > 0:
            return [cls.shelf_w, cls.shelf_h], np.zeros((cls.shelf_h, cls.shelf_w)).tolist(), []

        if len(decision_steps) == 0:
            cls.env.step()
            decision_steps, terminal_steps = cls.env.get_steps(cls.behavior_name)
            if len(terminal_steps) > 0 or len(decision_steps) == 0:
                return [cls.shelf_w, cls.shelf_h], np.zeros((cls.shelf_h, cls.shelf_w)).tolist(), []

        obs = decision_steps.obs[0][0]

        box_x = obs[-4] 
        box_y = obs[-3]
        box_z = obs[-2]
        target_shelf_id = int(obs[-1])
        
        grid_area = cls.shelf_w * cls.shelf_h
        box_weight = float(box_x * box_y * box_z * 10.0)
        cls.current_weight = box_weight

        # 🌟 타겟 선반(0 또는 1)의 1층과 2층 도면 데이터를 각각 추출
        floor1_start = (target_shelf_id * 2) * grid_area
        floor2_start = (target_shelf_id * 2 + 1) * grid_area
        
        floor1_grid = obs[floor1_start : floor1_start + grid_area]
        floor2_grid = obs[floor2_start : floor2_start + grid_area]

        # 1층과 2층의 사용률 계산
        floor1_usage = sum(floor1_grid) / grid_area
        floor2_usage = sum(floor2_grid) / grid_area

        # 🎯 스마트 라우팅 알고리즘 (평균 무게 2.5kg 기준)
        floor_offset = 0 # 기본값 1층(0)

        if box_weight >= 2.5: # 무거운 상자
            if floor1_usage < 0.8:  
                floor_offset = 0
            else:                   
                floor_offset = 1
        else:                 # 가벼운 상자
            if floor2_usage < 0.8:  
                floor_offset = 1
            else:                   
                floor_offset = 0

        # 🌟 결정된 층수를 나중에 쓰기 위해 클래스 변수에 저장
        cls.current_floor_offset = floor_offset

        # 선택된 층의 도면만 잘라서 RL 에이전트(테트리스 장인)에게 전달
        final_grid_index = (target_shelf_id * 2) + floor_offset
        start_idx = final_grid_index * grid_area
        grid_flat = obs[start_idx : start_idx + grid_area]

        current_state = np.reshape(grid_flat, (cls.shelf_h, cls.shelf_w)).tolist()

        box_w_cells = max(1, int(round(box_x / 0.1)))
        box_l_cells = max(1, int(round(box_z / 0.1)))
        
        boxes = [[box_w_cells, box_l_cells, box_weight]]

        return [cls.shelf_w, cls.shelf_h], current_state, boxes

    @classmethod
    def execute_step(cls, action_history):
        if not action_history:
            cls.env.step()
            return

        for action in action_history:
            # 파이썬 RL이 내놓은 3가지 행동(좌표, 회전)
            x, y, rotation = action
            actionX = ((x + 0.5) / cls.shelf_w) * 2.0 - 1.0
            actionZ = ((y + 0.5) / cls.shelf_h) * 2.0 - 1.0

            action_tuple = ActionTuple(
                continuous=np.array([[actionX, actionZ]], dtype=np.float32),
                # 🌟 [핵심] 회전 정보와 함께 매니저가 고른 층수(current_floor_offset)를 전송!
                discrete=np.array([[1 if rotation else 0, cls.current_floor_offset]], dtype=np.int32)
            )
        
            cls.env.set_actions(cls.behavior_name, action_tuple)
            cls.env.step()

    @classmethod
    def get_result(cls): return [[0, 0, 0, 0]]

    @classmethod
    def get_result_episode(cls): return [[0, 0, 0]]