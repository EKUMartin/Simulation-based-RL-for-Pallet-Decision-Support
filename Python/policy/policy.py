import torch
import torch.nn as nn
import torch.nn.functional as F

class ActorCritic(nn.Module):
    def __init__(self, grid_h, grid_w, output_size):
        super(ActorCritic, self).__init__()
        self.h = grid_h
        self.w = grid_w
        self.cnn = nn.Sequential(
            nn.Conv2d(3, 16, kernel_size=3, stride=1, padding=1),
            nn.ReLU(),
            nn.Conv2d(16, 32, kernel_size=3, stride=1, padding=1),
            nn.ReLU(),
            nn.Flatten()
        )
        cnn_out_size = 32 * grid_h * grid_w
        box_info_size = 3
        self.fc_actor = nn.Sequential(
            nn.Linear(cnn_out_size + box_info_size, 256),
            nn.ReLU(),
            nn.Linear(256, 128),
            nn.ReLU(),
            nn.Linear(128, output_size)
        )
        
        
        self.fc_critic = nn.Sequential(
            nn.Linear(cnn_out_size + box_info_size, 256),
            nn.ReLU(),
            nn.Linear(256, 128),
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
        box_info = data[:, 3 * grid_size:]

        current_state_2d = current_state_flat.view(-1, 1, self.h, self.w)
        fm_2d = fm_flat.view(-1, 2, self.h, self.w)
        grid_data = torch.cat((current_state_2d, fm_2d), dim=1)

        cnn_features = self.cnn(grid_data)
        combined = torch.cat((cnn_features, box_info), dim=1)
        x = self.fc_actor(combined)

        masked_logits = x.masked_fill(feasibility_map == 1, -1e9) # feasiblity, 1= not feasibile
        value = self.fc_critic(combined)
        
        if is_1d:
            masked_logits = masked_logits.squeeze(0)
            value = value.squeeze(0)
        return masked_logits,value