# Farming
Edgeサーバで動作するコンテナを管理するソフトです。Farming自体Docker上で実行します。
Farmingが動作しているDockerのコンテナをjsonファイルで記述した通りにコンテナを構成します。

凄くシンプルなk8sと言ったところでしょうか(大げさ)<br/>
<br/>

## ！注意！ 
構成ファイルに記述していないコンテナは削除されるので注意してください

# QuickStart
## 構成ファイルを記述します
ContainerSetting.json(この例ではnginxとmongodbが起動します)
```
{
    "ContainerSettings" : [
      {
          "Image" : "nginx",
          "Tag" : "latest",
          "Ports" : [
              "80/tcp:80"
          ],
          "Volumes" : [
              "/docker/nginx:/usr/share/nginx/html:ro"
          ]
      },
      {
        "Image": "mongo",
        "Tag": "latest",
        "Ports": [
          "27017/tcp:27017",
          "28017/tcp:28017"
        ],
        "Volumes": [
          "/mongodb:/data/db:rw"
        ],
        "Envs": [
          "MONGO_INITDB_ROOT_USERNAME=root",
          "MONGO_INITDB_ROOT_PASSWORD=rootpass"
        ]
      }
    ]
}
```
複数のコンテナが記述可能です。
* Image</br>
    コンテナ名
* Tag</br>
    タグ名
* Ports</br>
    公開ポート(公開するポートにプロトコル名があるので注意)
* Volumes</br>
    ボリューム（コンテナは削除される場合があるので、必ず永続化してください）
* Envs</br>
    環境変数
が指定出来ます

## Farming設定
* URI<br/> 
    ContainerSetting.jsonの場所を指定
* WaitTime<br/> 
    １処理（不要コンテナ削除、必要コンテナ起動）後のスリープmsec
* ContainerRemove<br/> 
    true→不要コンテナ停止＆削除<br/>
    false→不要コンテナ停止のみ
* Ignore<br/> 
    Farming外で実行したいコンテナイメージ名

## Farming実行
DockerでFarmingを実行してください
### docker cli
```
docker run -e URI=https://hogehoge.net/ContainerSetting.json -v /var/run/docker.sock:/var/run/docker.sock k.yamamoto03/farming:latest
```
実行時の注意
/var/run/docker.sock:/var/run/docker.sockをマウントしてください。
これによりFarmingがDockerAPI経由でアクセス可能となります

### docker-compose
```
version: '3.6'

services:

  farming:
    image: kyamamoto03/farming:latest
    restart: always
    environment:
      URI: https://hogehoge.net/ContainerSetting.json
      WaitTime: 10000
      ContainerRemove: 'True'
      Ignore: "Influxdb"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
```

# Farmingの特徴
.Net5で記述しています。内部では[Docker.DotNet](https://github.com/dotnet/dotnet-docker)を経由しDockerAPIにアクセスしています。
コンテナ情報をHTTP(s)で取得するのでリモートからFarmingの構成情報を変更が可能。
エッジにFarmigを入れると、リモートでコンテナの起動、停止、アップデートが可能です。
