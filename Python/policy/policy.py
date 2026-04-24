import torch
import torch.nn as nn
import torch.nn.functional as F

class ActorCritic(nn.Module):
    def __init__(self,input_size,output_size):
        super(ActorCritic, self).__init__()
        
        self.fc_actor=nn.Sequential(
            nn.Linear(input_size,256),
            nn.ReLU(),
            nn.Linear(256,128),
            nn.ReLU(),
            nn.Linear(128,output_size)
            )
        
        
        self.fc_critic=nn.Sequential(
            nn.Linear(input_size,256),
            nn.ReLU(),
            nn.Linear(256,128),
            nn.ReLU(),
            nn.Linear(128,1)
        )


    def forward(self, data,feasiblity_map):
        x=self.fc_actor(data)
        masked_logits = x.masked_fill(feasiblity_map == 0, -1e9) # feasiblity 0= not feasibile
        value=self.fc_critic(data)
        return masked_logits,value