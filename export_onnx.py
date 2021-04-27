from    export_parameters import ExportParameters

def export_onnx(prm: ExportParameters):
    """Esportazione ONNX

    Export model in ONNX format

    Args:
        prm: Export parameters
    """
    import  os
    import  sys
    from    tf2onnx import convert
    argv_save = sys.argv
    try:
        if (prm):
            sys.argv = [__file__, '--saved-model']
            sys.argv.append(os.path.join(prm.output_directory, 'saved_model'))
            sys.argv.append('--output')
            sys.argv.append(os.path.join(prm.output_directory, 'saved_model.onnx'))
            sys.argv.append('--opset')
            sys.argv.append('12')
        convert.main()
    finally:
        sys.argv = argv_save
    pass

if __name__ == '__main__':
    prm = ExportParameters()
    export_onnx(prm)

