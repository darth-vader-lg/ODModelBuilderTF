try:    from base_parameters import BaseParameters
except: pass
import  subprocess
import  sys
import  time
try:    from utilities import *
except: pass

def start_tensorboard(prm: BaseParameters):
    log_dir = prm.model_dir
    error = True
    paths = [
        'tensorboard',
        os.path.join(os.path.dirname(sys.executable), 'tensorboard'),
        os.path.join(getattr(sys, '_MEIPASS', sys.executable), 'tensorboard')]
    for tensorboard_path in paths:
        try:
            cmd = [tensorboard_path]
            cmd.extend(['--port', str(prm.tensorboard_port)])
            cmd.extend(['--logdir', log_dir])
            subprocess.Popen(cmd, stdout = subprocess.PIPE, universal_newlines = True)
            error = False
            break
        except:
            pass
    if (error):
        print('Warning: cannot start tensorboard')

if __name__ == '__main__':
    prm = ('prm' in locals() and isinstance(prm, BaseParameters) and prm) or BaseParameters.default
    start_tensorboard(prm)
