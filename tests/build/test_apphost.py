import json
from pathlib import Path
import subprocess
import unittest

REPOSITORY = Path(__file__).resolve().parents[2]


class AppHostTests(unittest.TestCase):
    def properties(self, project, host='arch-x64', portable='linux-x64', **overrides):
        arguments = ['dotnet', 'msbuild', str(REPOSITORY / project), '-nologo',
                     '-getProperty:RuntimeIdentifier,DefaultAppHostRuntimeIdentifier,UseAppHost',
                     f'-p:NETCoreSdkRuntimeIdentifier={host}',
                     f'-p:NETCoreSdkPortableRuntimeIdentifier={portable}']
        arguments.extend(f'-p:{name}={value}' for name, value in overrides.items())
        result = subprocess.run(arguments, cwd=REPOSITORY, check=True, capture_output=True, text=True)
        return json.loads(result.stdout)['Properties']

    def test_arch_uses_portable_apphost_for_both_executables(self):
        for project in ('src/Algetric.Desktop', 'tests/Algetric.Smoke'):
            with self.subTest(project=project):
                properties = self.properties(project)
                self.assertEqual('linux-x64', properties['DefaultAppHostRuntimeIdentifier'])
                self.assertEqual('', properties['RuntimeIdentifier'])
                self.assertEqual('true', properties['UseAppHost'])

    def test_explicit_publish_runtime_is_preserved(self):
        for runtime in ('linux-x64', 'win-x64'):
            with self.subTest(runtime=runtime):
                properties = self.properties('src/Algetric.Desktop', RuntimeIdentifier=runtime)
                self.assertEqual(runtime, properties['RuntimeIdentifier'])

    def test_windows_and_explicit_apphost_are_preserved(self):
        properties = self.properties('src/Algetric.Desktop', host='win-x64', portable='win-x64')
        self.assertEqual('win-x64', properties['DefaultAppHostRuntimeIdentifier'])
        properties = self.properties('src/Algetric.Desktop', DefaultAppHostRuntimeIdentifier='linux-arm64')
        self.assertEqual('linux-arm64', properties['DefaultAppHostRuntimeIdentifier'])


if __name__ == '__main__':
    unittest.main()
