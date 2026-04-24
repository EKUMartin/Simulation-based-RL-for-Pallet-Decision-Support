
class connect_unity:
    def reset():
        """
        시뮬레이션에서 초기 상태로 초기화
        """
    
    def get_observation():
        """
        시뮬에서 관측 가져오기
        가져올 값:
        1. shelf width height
            output:[width,height]
        2. current utilisation of shelf represented in matrix form
            occupied:1, free:0
            output:[[1,1,0,0],[1,1,0,0]]
            shelf divided into grid
            number of cell=width,height
        3. List of Boxes' information
            width,length,weight
            output:[box1,box2,box3]
        
        """
    
    def get_result():
        """
        time, space utilized, free space, distance traveled
        output:[box1,box2, box3]
        """

    def execute_step():
        """
        toss the action to simulation
        """
    