# Ÿ�� �ü���� ��������� �ϰ� ������ ����
cd Introducer 
dotnet publish -c release -r debian-arm64 --self-contained

# ����� ���ϵ��� �ִ� ��η� �̵�
cd bin\Release\net6.0\debian-arm64

# ��������
$compress = @{
LiteralPath= "publish"
CompressionLevel = "Fastest"
DestinationPath = "./publish.zip"
}

Compress-Archive @compress -Force

# �� ���� Ŭ���� ����ӽ��� Ȩ ���丮�� �ű�
gcloud compute scp publish.zip instance-2:publish.zip

# ===========================================
# ���� �ӽſ��� ���� Ǭ �� ����
# 
# rm -rf publish
# cd ~ && unzip publish.zip -d .
# cd publish && sudo chmod +x ./Introducer
# ./Introducer
#
# ===========================================