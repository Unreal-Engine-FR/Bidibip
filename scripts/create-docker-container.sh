#!/bin/sh

bidibip=`sudo docker ps -a | grep bidibip`

# Stop existing image
[ -z "$bidibip" ] || {
  echo "A bidibip container is already running and will be permanently erased"
  read -p "Continue (y/N)?" choice
  case "$choice" in
    y|Y )
      bidibip_id=`echo $bidibip | awk '{ print $1 }'`
      echo -n 'stop container... '
      sudo docker stop $bidibip_id
      echo -n 'delete container... '
      sudo docker rm $bidibip_id;;
    * ) return 0;;
  esac
}

echo -n 'create new container... '
sudo docker run -d --restart unless-stopped --mount source=bidibip-saved,target=/home/node/Bidibip/saved/ bidibip