If(-not(Get-InstalledModule 7Zip4PowerShell -ErrorAction silentlycontinue)){
    Install-Module 7Zip4PowerShell -Confirm:$False -Force
}

$filePath = 'FileConvert\bin\Release\netstandard2.1\publish\FileConvert\dist'
cd $filePath

$files = Get-ChildItem  -recurse -filter *.*
foreach ($file in $files) {Compress-7Zip -path $file -ArchiveFileName "$file.gz" -Format GZip}
Get-ChildItem -filter *.*  -recurse -Exclude *.gz| Remove-Item
Get-ChildItem -filter *.* -recurse | Rename-Item -NewName { $_ -replace '\.gz','' }

cd '..\..\..\..\..\..\..'

