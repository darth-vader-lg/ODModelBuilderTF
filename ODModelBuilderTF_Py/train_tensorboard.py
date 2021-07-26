try:    from base_parameters import BaseParameters
except: pass
import  os
import  subprocess
import  sys

tensorboard_process = None
tensorboard_start_count = 0

def start_tensorboard(prm: BaseParameters):
    if (prm.tensorboard_port <= 0 or not prm.model_dir):
        return
    global tensorboard_process
    if (tensorboard_process):
        return;
    try:
        cmd = [sys.executable, '-m', 'tensorboard.main']
        cmd.extend(['--port', str(prm.tensorboard_port)])
        cmd.extend(['--logdir', prm.model_dir])
        env = { **os.environ, 'PATH': os.pathsep.join(sys.path) + os.pathsep + os.environ['PATH'] }
        creationflags = subprocess.CREATE_NO_WINDOW if 'CREATE_NO_WINDOW' in dir(subprocess) else 0
        p = subprocess.Popen(cmd, env=env, cwd=os.getcwd(), stdout=subprocess.PIPE, stderr=subprocess.PIPE, universal_newlines=True,shell=False,creationflags=creationflags)
        p_result = p.poll()
        if (p_result != None):
            print(f'Warning: cannot start tensorboard. Error code {p_result}')
            return None
        else:
            tensorboard_process = p
            return p
    except Exception as exc:
        print(f'Warning: cannot start tensorboard. Exception {exc}')
    return None

def stop_tensorboard(process):
    global tensorboard_process
    if (not tensorboard_process or tensorboard_process != process):
        return
    tensorboard_process.terminate()
    tensorboard_process.wait()
    tensorboard_process = None

if __name__ == '__main__':
    prm = ('prm' in locals() and isinstance(prm, BaseParameters) and prm) or BaseParameters.default
    start_tensorboard(prm)
