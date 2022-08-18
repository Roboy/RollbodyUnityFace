import socket
import json
import time
import pygame


# Find out everything about TCP connections in Python here:
# https://realpython.com/python-sockets/#echo-server

class TCPTestServer:
    """
    TCP server to be used for communicating with the RollbodyUnityFace simulation.

    Usage example:

    """
    def __init__(self, host="127.0.0.1", port=8052):
        """
        Standard loopback interface address (localhost)
        Port to listen on (non-privileged ports are > 1023)
        """
        self.host = host
        self.port = port
        self.socket = None
        self.conn = None

    def start(self, restart_count=0):
        self.server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        try:
            self.server.bind((self.host, self.port))
            self.server.listen()
            print(f"Created server on host {self.host} and port {self.port}. Waiting for connection...")
            self.conn, addr = self.server.accept()
            print(f"Connected by {addr}")
        except socket.error as e:
            n_retries = 20
            if e.errno == 48: # OSError: [Errno 48] Address already in use
                if restart_count <= n_retries:
                    print(f"Caught: {e}. Restarting ({restart_count}/{n_retries})")
                    time.sleep(1)
                    return self.start(restart_count+1)
                else:
                    print(f"Could not reconnect after {n_retries} retries. Try another port for the TCP client and server or restart both.")
                    exit()
            else:
                raise e

    def close(self):
        try:
            self.socket.close()
        except Exception as e:
            print(f"Received exception during TCP connection shutdown: {e}")

    def update(self, **msg_dict):
        # data = str(self.conn.recv(1024))    # blocks until msg is received
        # if not data:
        #     return False
        self.send_msg(**msg_dict)
        return True

    def send_msg(self, head_roll=0., head_yaw=0., head_pitch=0., is_talking=False, emotion=""):
        msg = f"headRoll={head_roll};headYaw={head_yaw};headPitch={head_pitch};isTalking={is_talking};emotion={emotion}"
        # print("sending: " + str(msg))
        # msg = json.dumps(msg).encode("utf-8")   # converts to bytes
        try:
            self.conn.sendall(bytes(msg, encoding='utf8'))
        except socket.error as e:
            if e.errno == 32:   # Broken pipe
                print(f"Caught: {e}. Restarting TCP server.")
                return self.start()
            print(f"Caught: {e}")


if __name__ == '__main__':

    server = TCPTestServer()
    server.start()

  
    screen = pygame.display.set_mode((300, 300))
    pygame.display.set_caption('WindowToRegisterInputs')
    screen.fill((234, 212, 252))
    pygame.display.flip()
    pygame.init()
    clock = pygame.time.Clock()
    x, y, z = 0, 0, 0
    while True:
        # IMPORTANT: Set approiate FPS (too large -> TCP connection breaks)
        clock.tick(30)
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                pygame.quit()
                quit()

        pygame.event.pump() # update the keyboard state in memory
        keys = pygame.key.get_pressed()
        if keys[pygame.K_x]:
            x = x + 1 if x + 1 < 360 else 0            
        elif keys[pygame.K_y]:
            y = y + 1 if y + 1 < 360 else 0            
        elif keys[pygame.K_z]:
            z = z + 1 if z + 1 < 360 else 0      

        print(f"(x, y, z): ({x},{y},{z})")      

        server.update(head_roll=x, head_yaw=y, head_pitch=z)
        