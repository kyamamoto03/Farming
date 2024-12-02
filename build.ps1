dotnet clean -c Debug
dotnet clean -c Release
 
$containerName = 'iforcomenergysolution/farming'
$dockerCmd = 'docker build -t {0}:{1} .' -f $containerName,$args[0]
cmd /c $dockerCmd
 
$dockerPushCmd = 'docker push {0}:{1}' -f $containerName,$args[0]
cmd /c $dockerPushCmd