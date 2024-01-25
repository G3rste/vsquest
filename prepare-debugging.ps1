$modinfo=Get-Content -Path resources\modinfo.json | ConvertFrom-Json
$modid=$modinfo.modid;
Remove-Item "bin/vsdebug" -Force -Recurse; 
New-Item -Path "bin/vsdebug" -Name $modid -ItemType "directory"; 
Copy-Item -Path "resources/*" -Destination "bin/vsdebug/$modid" -Recurse;
Copy-Item -Path "bin/Debug/net7.0/*" -Destination "bin/vsdebug/$modid";