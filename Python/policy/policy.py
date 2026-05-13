import torch
import torch.nn as nn
import torch.nn.functional as F

class ActorCritic(nn.Module):
    def __init__(self, grid_h, grid_w, output_size):
        super(ActorCritic, self).__init__()
        self.h = grid_h
        self.w = grid_w

        self.feature_extractor = nn.Sequential(
            nn.Conv2d(3, 32, kernel_size=3, padding=1),
            nn.ReLU(),
            nn.Conv2d(32, 64, kernel_size=3, padding=1),
            nn.ReLU(),
            nn.Conv2d(64, 64, kernel_size=3, padding=1), 
            nn.ReLU(),
            nn.Conv2d(64, 64, kernel_size=3, padding=1), 
            nn.ReLU()
        )
        self.box_info_dim = 8

        self.actor_head = nn.Sequential(
            nn.Conv2d(64 + self.box_info_dim, 32, kernel_size=1),
            nn.ReLU(),
            nn.Conv2d(32, 2, kernel_size=1) # 채널 0:회전X, 채널 1:90도 회전
        )
        
        self.critic_pool = nn.AdaptiveAvgPool2d(1)
        self.critic_linear = nn.Sequential(
            nn.Linear(64 + self.box_info_dim, 128),
            nn.ReLU(),
            nn.Linear(128, 1)
        )

    def forward(self, data, feasibility_map):
        is_1d = data.dim() == 1
        if is_1d:
            data = data.unsqueeze(0)
            feasibility_map = feasibility_map.unsqueeze(0)
            
        grid_size = self.h * self.w
        current_state_flat = data[:, :grid_size]
        fm_flat = data[:, grid_size : 3 * grid_size]
        box_info = data[:, 3 * grid_size:] # (Batch, 3)


        current_state_2d = current_state_flat.view(-1, 1, self.h, self.w)
        fm_2d = fm_flat.view(-1, 2, self.h, self.w)
        grid_data = torch.cat((current_state_2d, fm_2d), dim=1) # (Batch, 3, H, W)

   
        spatial_features = self.feature_extractor(grid_data) # (Batch, 64, H, W)

        # (Batch, 3) -> (Batch, 3, H, W)
        box_info_spatial = box_info.view(-1, 8, 1, 1).expand(-1, 8, self.h, self.w)
        

        actor_input = torch.cat((spatial_features, box_info_spatial), dim=1)
        logits_2d = self.actor_head(actor_input) # (Batch, 2, H, W)
        logits = logits_2d.view(data.size(0), -1) # 다시 flatten하여 Categorical 분포용으로 변환

        # Masking
        masked_logits = logits.masked_fill(feasibility_map == 1, -1e9)

        pooled_spatial = self.critic_pool(spatial_features).view(-1, 64)
        critic_input = torch.cat((pooled_spatial, box_info), dim=1)
        value = self.critic_linear(critic_input)
        
        if is_1d:
            masked_logits = masked_logits.squeeze(0)
            value = value.squeeze(0)

        # return logits, value    
        return masked_logits, value