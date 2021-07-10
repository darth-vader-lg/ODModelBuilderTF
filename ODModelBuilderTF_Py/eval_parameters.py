import  os

try:    from    base_parameters import BaseParameters
except: pass
try:    from    default_cfg import Cfg
except: pass

class EvalParameters(BaseParameters):
    """ Class holding the train execution parameters """
    def __init__(self):
        """ Constructor """
        super().__init__()
        self._pipeline_config_path = os.path.join(self.annotations_dir, 'pipeline.config')
        self._sample_1_of_n_eval_examples = None
        self._sample_1_of_n_eval_on_train_examples = 5
        self._checkpoint_dir = None
        self._eval_timeout = 3600
        self._wait_interval = 300
        self._tensorboard_port = 8080
        self._is_path.extend([
            'pipeline_config_path',
            'checkpoint_dir'])
    default = None
    @property
    def pipeline_config_path(self): return self._pipeline_config_path
    @pipeline_config_path.setter
    def pipeline_config_path(self, value): self._pipeline_config_path = value
    @property
    def sample_1_of_n_eval_examples(self): return self._sample_1_of_n_eval_examples
    @sample_1_of_n_eval_examples.setter
    def sample_1_of_n_eval_examples(self, value): self._sample_1_of_n_eval_examples = value
    @property
    def sample_1_of_n_eval_on_train_examples(self): return self._sample_1_of_n_eval_on_train_examples
    @sample_1_of_n_eval_on_train_examples.setter
    def sample_1_of_n_eval_on_train_examples(self, value): self._sample_1_of_n_eval_on_train_examples = value
    @property
    def checkpoint_dir(self): return self._checkpoint_dir
    @checkpoint_dir.setter
    def checkpoint_dir(self, value): self._checkpoint_dir = value
    @property
    def wait_interval(self): return self._wait_interval
    @wait_interval.setter
    def wait_interval(self, value): self._wait_interval = value
    @property
    def eval_timeout(self): return self._eval_timeout
    @eval_timeout.setter
    def eval_timeout(self, value): self._eval_timeout = value
    @property
    def tensorboard_port(self): return self._tensorboard_port
    @tensorboard_port.setter
    def tensorboard_port(self, value): self._tensorboard_port = value

EvalParameters.default = EvalParameters.default or EvalParameters()

if __name__ == '__main__':
    prm = ('prm' in locals() and isinstance(prm, EvalParameters) and prm) or EvalParameters.default
    print(prm)
    print('Eval parameters configured')
