# 타겟 운영체제를 데비안으로 하고 릴리즈 빌드
cd Introducer 
dotnet publish -c release -r debian-arm64 --self-contained

# 빌드된 파일들이 있는 경로로 이동
cd bin\Release\net6.0\debian-arm64

# 압축진행
$compress = @{
LiteralPath= "publish"
CompressionLevel = "Fastest"
DestinationPath = "./publish.zip"
}

Compress-Archive @compress -Force

# 내 구글 클라우드 가상머신의 홈 디렉토리로 옮김
gcloud compute scp publish.zip instance-2:publish.zip

# ===========================================
# 가상 머신에서 압축 푼 후 실행
# 
# rm -rf publish
# cd ~ && unzip publish.zip -d .
# cd publish && sudo chmod +x ./Introducer
# ./Introducer
#
# ===========================================