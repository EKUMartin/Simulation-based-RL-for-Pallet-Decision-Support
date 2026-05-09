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
    last_valid_state = None 

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
            cls.last_valid_state = None

    @classmethod
    def get_observation(cls):
        """유니티가 보낸 1개 층의 최적화된 데이터를 지도로 복원"""
        decision_steps, terminal_steps = cls.env.get_steps(cls.behavior_name)

        if len(terminal_steps) > 0:
            fallback_state = cls.last_valid_state if cls.last_valid_state is not None else np.zeros((cls.shelf_h, cls.shelf_w)).tolist()
            return [cls.shelf_w, cls.shelf_h], fallback_state, []

        if len(decision_steps) == 0:
            cls.env.step()
            decision_steps, terminal_steps = cls.env.get_steps(cls.behavior_name)
            if len(terminal_steps) > 0 or len(decision_steps) == 0:
                fallback_state = cls.last_valid_state if cls.last_valid_state is not None else np.zeros((cls.shelf_h, cls.shelf_w)).tolist()
                return [cls.shelf_w, cls.shelf_h], fallback_state, []

        obs = decision_steps.obs[0][0]

        # 데이터 끝에서 박스 규격 추출
        box_x = obs[-4] 
        box_y = obs[-3]
        box_z = obs[-2]
        # target_shelf_id = obs[-1] (유니티에서 전체 층을 탐색하므로 이제 파이썬에선 쓰지 않지만 통신 규격을 위해 수신은 합니다)
        
        grid_area = cls.shelf_w * cls.shelf_h
        box_weight = float(box_x * box_y * box_z * 10.0)
        cls.current_weight = box_weight

        # 🌟 유니티가 딱 1개 층만 보냈으므로, 처음부터 grid_area까지만 자르면 됩니다.
        grid_flat = obs[:grid_area] 
        current_state = np.reshape(grid_flat, (cls.shelf_h, cls.shelf_w)).tolist()
        cls.last_valid_state = current_state

        box_w_cells = max(1, int(round(box_x / 0.1)))
        box_l_cells = max(1, int(round(box_z / 0.1)))
        
        boxes = [[box_w_cells, box_l_cells, box_weight]]

        return [cls.shelf_w, cls.shelf_h], current_state, boxes

    @classmethod
    def execute_step(cls, action_history):
        """정수형 좌표를 유니티로 다이렉트 전송"""
        if not action_history:
            cls.env.step()
            return

        for action in action_history:
            x, y, rotation = action
            
            # 연속형 데이터 사용 안 함
            continuous_actions = np.empty((1, 0), dtype=np.float32)
            
            # 🌟 [X좌표, Z좌표, 회전여부] 딱 3개의 이산형(정수) 데이터만 포장
            discrete_actions = np.array([[x, y, 1 if rotation else 0]], dtype=np.int32)

            action_tuple = ActionTuple(
                continuous=continuous_actions,
                discrete=discrete_actions
            )
        
            cls.env.set_actions(cls.behavior_name, action_tuple)
            cls.env.step()

    @classmethod
    def get_result(cls): return [[0, 0, 0, 0]]

    @classmethod
    def get_result_episode(cls): return [[0, 0, 0]]