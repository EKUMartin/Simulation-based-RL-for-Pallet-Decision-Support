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
        """유니티에서 데이터를 가져와서 11x152 지도로 복원"""
        decision_steps, terminal_steps = cls.env.get_steps(cls.behavior_name)

        if len(terminal_steps) > 0:
            return [cls.shelf_w, cls.shelf_h], np.zeros((cls.shelf_h, cls.shelf_w)).tolist(), []

        if len(decision_steps) == 0:
            cls.env.step()
            decision_steps, terminal_steps = cls.env.get_steps(cls.behavior_name)
            if len(terminal_steps) > 0 or len(decision_steps) == 0:
                return [cls.shelf_w, cls.shelf_h], np.zeros((cls.shelf_h, cls.shelf_w)).tolist(), []

        obs = decision_steps.obs[0][0]

        # 박스 정보 추출
        box_x, box_y, box_z = obs[-4], obs[-3], obs[-2]
        target_shelf_id = int(obs[-1])

        grid_area = cls.shelf_w * cls.shelf_h

        # 1층 데이터를 가져와서 152행 11열로 재구성
        start_idx = (target_shelf_id * 2) * grid_area
        grid_flat = obs[start_idx : start_idx + grid_area]
        
        # 1672개의 데이터를 (152, 11) 형상으로 접습니다.
        current_state = np.reshape(grid_flat, (cls.shelf_h, cls.shelf_w)).tolist()

        # 박스 크기 및 무게 계산
        box_w_cells = int(round(box_x / 0.1))
        box_l_cells = int(round(box_z / 0.1))
        box_weight = float(box_y * 10.0)
        
        cls.current_weight = box_weight
        boxes = [[box_w_cells, box_l_cells, box_weight]]

        return [cls.shelf_w, cls.shelf_h], current_state, boxes

    @classmethod # 🌟 3. 중복 데코레이터 삭제 완료!
    def execute_step(cls, action_history):
        """에이전트의 행동을 유니티로 전송"""
        if not action_history:
            cls.env.step()
            return

        x, y, rotation = action_history[0]

        # 11 x 152 좌표계에 맞춰 정규화 ($ -1.0 \sim 1.0 $)
        actionX = ((x + 0.5) / cls.shelf_w) * 2.0 - 1.0
        actionZ = ((y + 0.5) / cls.shelf_h) * 2.0 - 1.0

        continuous_actions = np.array([[actionX, actionZ]], dtype=np.float32)
        
        # 🌟 4. 무게 기준 5.0kg으로 1층/2층 배분
        THRESHOLD = 5.0 
        floor = 1 if cls.current_weight < THRESHOLD else 0
        floor_name = "2층 (가벼움)" if floor == 1 else "1층 (무거움)"

        discrete_actions = np.array([[floor]], dtype=np.int32)
        action_tuple = ActionTuple(continuous=continuous_actions, discrete=discrete_actions)
        
        cls.env.set_actions(cls.behavior_name, action_tuple)
        cls.env.step()

    @classmethod
    def get_result(cls): return [[0, 0, 0, 0]]

    @classmethod
    def get_result_episode(cls): return [[0, 0, 0]]