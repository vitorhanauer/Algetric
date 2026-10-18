import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import time
import unittest

REPOSITORY = Path(__file__).resolve().parents[2]


class InstallationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='algetric-install-')
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.bundle = self.root / 'package'
        self.bundle.mkdir()
        for source, target in [('install-linux.sh', 'install.sh'),
                               ('uninstall-linux.sh', 'uninstall.sh'),
                               ('linux-common.sh', 'linux-common.sh')]:
            shutil.copyfile(REPOSITORY / 'scripts' / source, self.bundle / target)
        shutil.copyfile(REPOSITORY / 'assets/algetric.svg', self.bundle / 'algetric.svg')
        executable = self.bundle / 'Algetric.Desktop'
        executable.write_text('#!/bin/sh\nprintf "%s\\n" "$@" > "$XDG_DATA_HOME/launch.log"\n')
        executable.chmod(0o755)
        (self.bundle / 'Web/vendor/Cesium').mkdir(parents=True)
        (self.bundle / 'Web/vendor/Cesium/Cesium.js').write_text('// local assets')
        self.data = self.root / 'data with spaces and \'quotes $cash %rate=100'
        self.bin = self.root / 'local bin'
        self.environment = dict(os.environ, XDG_DATA_HOME=str(self.data), ALGETRIC_BIN_DIR=str(self.bin))

    def run_script(self, name, *arguments, success=True):
        result = subprocess.run(['bash', str(self.bundle / name), *arguments],
                                env=self.environment, text=True, capture_output=True)
        if success:
            self.assertEqual(0, result.returncode, result.stderr)
        else:
            self.assertNotEqual(0, result.returncode)
        return result

    def test_installs_independent_copy_and_launchable_desktop_entry(self):
        self.run_script('install.sh')
        entry = self.data / 'applications/algetric.desktop'
        self.assertIn('Terminal=false', entry.read_text())
        self.assertTrue((self.data / 'icons/hicolor/scalable/apps/algetric.svg').is_file())
        if shutil.which('desktop-file-validate'):
            subprocess.run(['desktop-file-validate', str(entry)], check=True)
        shutil.rmtree(self.bundle)
        subprocess.run([str(self.bin / 'algetric'), '--diagnostics'], env=self.environment, check=True)
        self.assertEqual('--diagnostics\n', (self.data / 'launch.log').read_text())
        if shutil.which('gio'):
            (self.data / 'launch.log').unlink()
            subprocess.run(['gio', 'launch', str(entry)], env=self.environment, check=True)
            for _ in range(100):
                if (self.data / 'launch.log').exists():
                    break
                time.sleep(0.02)
            self.assertTrue((self.data / 'launch.log').exists(), 'Desktop launcher did not start the application')

    def test_reinstall_updates_binary_and_uninstall_preserves_user_data(self):
        self.run_script('install.sh')
        cache = self.data / 'algetric/maps.sqlite'
        cache.write_text('preserved cache')
        (self.bundle / 'version.txt').write_text('new version')
        self.run_script('install.sh')
        installed = self.data / 'algetric/application'
        self.assertEqual('new version', (installed / 'version.txt').read_text())
        subprocess.run(['bash', str(installed / 'uninstall.sh')], env=self.environment, check=True)
        self.assertEqual('preserved cache', cache.read_text())
        self.assertFalse(installed.exists())
        self.assertFalse((self.bin / 'algetric').is_symlink())
        self.assertFalse((self.data / 'applications/algetric.desktop').exists())
        self.run_script('uninstall.sh')

    def test_refuses_to_replace_an_unrelated_launcher(self):
        self.bin.mkdir()
        launcher = self.bin / 'algetric'
        launcher.write_text('unrelated executable')
        self.run_script('install.sh', success=False)
        self.assertEqual('unrelated executable', launcher.read_text())

    def test_missing_assets_leave_previous_installation_untouched(self):
        self.run_script('install.sh')
        shutil.rmtree(self.bundle / 'Web')
        self.run_script('install.sh', success=False)
        self.assertTrue((self.bin / 'algetric').is_file())


if __name__ == '__main__':
    unittest.main()
