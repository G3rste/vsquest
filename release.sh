#!/bin/bash
version=$(grep -o '"version":\s*"\([0-9]\.*\)*"' resources/modinfo.json | grep -o '\([0-9]\.*\)*')
releasefile='bin/vsquest_v'$version'.zip'
exampleversion=$(grep -o '"version":\s*"\([0-9]\.*\)*"' example/modinfo.json | grep -o '\([0-9]\.*\)*')
examplereleasefile='bin/vsquestexample_v'$exampleversion'_vsquest_v'$version'.zip'
dotnet build -c release
jar -cMf $examplereleasefile -C example .
mv bin/vsquest.zip $releasefile
gh release create --generate-notes 'v'$version $releasefile $examplereleasefile