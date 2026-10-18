#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
npm ci --ignore-scripts --prefix src/Algetric.Desktop/Web
npm run build --prefix src/Algetric.Desktop/Web
npm test --prefix src/Algetric.Desktop/Web
dotnet restore Algetric.sln --locked-mode
dotnet test tests/Algetric.Tests --no-restore -c Release -m:1
dotnet publish src/Algetric.Desktop -c Release -r linux-x64 --self-contained true -p:RestoreLockedMode=true -o artifacts/linux-x64
cp README.md THIRD_PARTY_NOTICES.md artifacts/linux-x64/
cp -r docs artifacts/linux-x64/
tar -czf artifacts/algetric-linux-x64.tar.gz -C artifacts/linux-x64 .
