import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

REPOSITORY = Path(__file__).resolve().parents[2]


class SourceLauncherTests(unittest.TestCase):
    def test_prepares_assets_once_and_forwards_arguments(self):
        with tempfile.TemporaryDirectory(prefix='algetric-run-') as directory:
            root = Path(directory)
            (root / 'scripts').mkdir()
            shutil.copyfile(REPOSITORY / 'scripts/run.sh', root / 'scripts/run.sh')
            commands = root / 'commands'
            commands.mkdir()
            npm = commands / 'npm'
            npm.write_text('''#!/bin/sh
printf '%s\\n' "$*" >> "$FAKE_NPM_LOG"
if [ "$1" = run ]; then
  mkdir -p src/Algetric.Desktop/Web/vendor/Cesium
  touch src/Algetric.Desktop/Web/vendor/Cesium/Cesium.js
fi
''')
            dotnet = commands / 'dotnet'
            dotnet.write_text('#!/bin/sh\nprintf "%s\\0" "$@" > "$FAKE_DOTNET_ARGS"\n')
            npm.chmod(0o755)
            dotnet.chmod(0o755)
            environment = dict(os.environ, PATH=str(commands) + os.pathsep + os.environ['PATH'],
                               FAKE_NPM_LOG=str(root / 'npm.log'), FAKE_DOTNET_ARGS=str(root / 'args'))
            invocation = ['bash', str(root / 'scripts/run.sh'), '--diagnostics', 'value with spaces']
            subprocess.run(invocation, cwd='/tmp', env=environment, check=True)
            self.assertEqual(2, len((root / 'npm.log').read_text().splitlines()))
            subprocess.run(invocation, cwd='/tmp', env=environment, check=True)
            self.assertEqual(2, len((root / 'npm.log').read_text().splitlines()))
            self.assertEqual([b'run', b'--project', b'src/Algetric.Desktop', b'--',
                              b'--diagnostics', b'value with spaces', b''], (root / 'args').read_bytes().split(b'\0'))


if __name__ == '__main__':
    unittest.main()
