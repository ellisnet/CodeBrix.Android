#!/usr/bin/env python3
"""Run UIReqs on an already connected device without changing its system configuration.

The activity requests its own orientation. No AVD is created, started, stopped or
reconfigured. Display-resize scenarios fail at the host shell boundary; IME-reset
isolation is explicitly unverified. Each group gets a fresh application process.
"""
import argparse
import fcntl
import os
from pathlib import Path
import struct
import subprocess
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--serial', required=True)
    parser.add_argument('--group', nargs='+', default=['Harness'])
    parser.add_argument('--orientation', choices=['Portrait', 'Landscape', 'both'], default='both')
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--no-build', action='store_true')
    parser.add_argument('--repeat', type=int, default=1)
    args = parser.parse_args()
    if args.repeat < 1:
        parser.error('--repeat must be positive')
    repo = Path(__file__).resolve().parents[2]
    work = Path.home() / 'ClaudeHome'
    adb = Path(os.environ.get('ANDROID_HOME', str(Path.home() / 'Android/Sdk'))) / 'platform-tools/adb'
    args.out.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, ANDROID_SERIAL=args.serial, MSBUILDDISABLENODEREUSE='1',
               UIREQS_SERIAL=args.serial, UIREQS_HOST_PORT='47300', UIREQS_PRESERVE_DEVICE_CONFIGURATION='1')

    def device(*command, check=True):
        return subprocess.run([str(adb), '-s', args.serial, *command], check=check,
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=30)

    def logged(label, command, run_env=None):
        with (args.out / (label + '.log')).open('w') as output:
            result = subprocess.run(command, cwd=repo, env=run_env or env,
                                    stdout=output, stderr=subprocess.STDOUT)
        print(f'{label}: exit {result.returncode}', flush=True)
        return result.returncode

    # Keep the established cross-repository lock order; child processes do not inherit the lock handles.
    with (work / 'platform-split-work/platform.lock').open('a') as platform_lock:
        fcntl.flock(platform_lock, fcntl.LOCK_EX)
        with (work / 'android-buildout-work/build.lock').open('a') as android_lock:
            fcntl.flock(android_lock, fcntl.LOCK_EX)
            if device('get-state').stdout.strip() != b'device':
                raise RuntimeError('The requested device is not connected.')
            abi = device('shell', 'getprop', 'ro.product.cpu.abi').stdout.decode().strip()
            rid = {'arm64-v8a': 'android-arm64', 'x86_64': 'android-x64'}[abi]
            settings = []
            for category, key in [('global', 'animator_duration_scale'), ('system', 'font_scale')]:
                value = device('shell', 'settings', 'get', category, key).stdout.decode().strip()
                settings.append(f'{category}.{key}={value}')
            for query in ['size', 'density']:
                settings.append(device('shell', 'wm', query).stdout.decode().strip())
            (args.out / 'device.txt').write_text(f'serial={args.serial}\nabi={abi}\n' + '\n'.join(settings) +
                '\nConfiguration preserved. IME-reset isolation unverified; resize requests are rejected.\n')
            flags = ['-p:EnableSourceControlManagerQueries=false', '-p:SourceLinkEnabled=false',
                     '-p:GeneratePackageOnBuild=false', '-m:4', '-nodeReuse:false']
            if not args.no_build:
                if logged('device-build', ['nice', '-n', '19', 'dotnet', 'build',
                        'tests/CodeBrix.Android.UIReqs.Device/CodeBrix.Android.UIReqs.Device.csproj',
                        '-c', 'Debug', '-t:Install', f'-p:AdbTarget=-s {args.serial}',
                        f'-p:RuntimeIdentifier={rid}', *flags]):
                    return 2
                if logged('host-build', ['nice', '-n', '19', 'dotnet', 'build',
                        'tests/CodeBrix.Android.UIReqs/CodeBrix.Android.UIReqs.csproj', '-c', 'Debug', *flags]):
                    return 2
            runner = repo / 'tests/CodeBrix.Android.UIReqs/bin/Debug/net10.0/CodeBrix.Android.UIReqs'
            groups = args.group
            if groups == ['all']:
                base = repo / 'tests/CodeBrix.Android.UIReqs.Device'
                groups = sorted({p.parent.name for root in ['AndroidFeatures', 'Scenarios/Features']
                                 for p in (base / root).glob('*/*.feature')})
            orientations = ['Portrait', 'Landscape'] if args.orientation == 'both' else [args.orientation]
            failures = 0
            try:
                for repeat in range(1, args.repeat + 1):
                    for orientation in orientations:
                        for group in groups:
                            label = f'run{repeat}-{orientation}-{group}'
                            device('shell', 'am', 'force-stop', 'com.codebrix.uireqs')
                            launch = device('shell', 'am', 'start', '-W', '-n',
                                            'com.codebrix.uireqs/com.codebrix.uireqs.MainActivity',
                                            '--es', 'uireqsOrientation', orientation, '--ez', 'preserveConfiguration', 'true')
                            (args.out / (label + '.launch.txt')).write_bytes(launch.stdout + launch.stderr)
                            # Verify the actual orientation; declaring an orientation is never enough.
                            matched = False
                            for _ in range(20):
                                shot = device('exec-out', 'screencap', '-p').stdout
                                if shot.startswith(b'\x89PNG\r\n\x1a\n'):
                                    width, height = struct.unpack('>II', shot[16:24])
                                    if (width > height) == (orientation == 'Landscape'):
                                        matched = True
                                        break
                                time.sleep(0.25)
                            if not matched:
                                print(f'{label}: activity orientation unavailable; no device settings changed.', flush=True)
                                failures += 1
                                continue
                            device('forward', 'tcp:47300', 'tcp:47300')
                            run_env = dict(env, UIREQS_ORIENTATION=orientation,
                                           CODEBRIX_UIREQS_FRAME_SAVE=str(args.out / f'frames{repeat}'))
                            failures += logged(label, [str(runner), '--filter-namespace',
                                'CodeBrix.Android.UIReqs.Features.' + group,
                                '--results-directory', str(args.out / ('results-' + label)), '--output', 'Detailed'], run_env) != 0
                            (args.out / (label + '.last.png')).write_bytes(device('exec-out', 'screencap', '-p').stdout)
                            pid = device('shell', 'pidof', 'com.codebrix.uireqs', check=False).stdout.decode().strip()
                            if pid.isdigit():
                                (args.out / (label + '.logcat.txt')).write_bytes(
                                    device('logcat', '-d', '--pid=' + pid, '-t', '2000').stdout)
                            device('forward', '--remove', 'tcp:47300', check=False)
            finally:
                device('forward', '--remove', 'tcp:47300', check=False)
                device('shell', 'am', 'force-stop', 'com.codebrix.uireqs', check=False)
            print(f'Finished: {failures} failed group runs. Configuration-dependent checks remain unverified.', flush=True)
            return 1 if failures else 0


if __name__ == '__main__':
    raise SystemExit(main())
