$fileNames = Get-ChildItem -Path $scriptPath -Recurse

foreach ($file in $fileNames)
{
    if ($file.Name.EndsWith("vert") -Or 
		$file.Name.EndsWith("frag") -Or 
		$file.Name.EndsWith("comp"))
    {
		$dstFileName = $file.Name + ".spv"
		
		if (Test-Path $dstFileName)
		{
			$dstFile = Get-Item $dstFileName
			if ($dstFile.LastWriteTime -eq $file.LastWriteTime)
			{
				Write-Host "Skipped $file"
				continue
			}
		}
		
        Write-Host "Compiling $file"
        .\glslangvalidator -V $file -o $dstFileName
		
		if (($LastExitCode -eq 0) -And (Test-Path $dstFileName))
		{
			$dstFile = Get-Item $dstFileName
			$dstFile.LastWriteTime = $file.LastWriteTime
		}
    }
}