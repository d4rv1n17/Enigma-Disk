"""Run after building HubLink.Checks; exercise the real Python/C# protocol."""
import os
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root.parent / 'Enigma Hub'))
from hub_ecosystem import LaunchBroker

checks = root / 'tests/HubLink.Checks/bin/Release/net10.0-windows/HubLink.Checks.dll'
broker = LaunchBroker()
try:
    environment = broker.environment('disk')
    def claim(env):
        result = subprocess.run(['dotnet', str(checks), '--claim'], env=env,
                                capture_output=True, text=True, timeout=15, check=True)
        return result.stdout.strip()
    assert claim(environment) == 'accepted', 'Real Disk client did not claim Hub launch'
    assert claim(environment) == 'rejected', 'A launch ticket was reused'
    assert claim(broker.environment('cube')) == 'rejected', 'Disk accepted a Cube ticket'
    print('Python Hub / C# Disk: accepted, replay rejected, wrong product rejected')
finally:
    broker.close()
