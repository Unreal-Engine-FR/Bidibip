#!/bin/sh

bidibip_image_id=`sudo docker images | grep bidibip | awk '{ print $1 }'`

# Stop existing image
[ -z "$bidibip_image_id" ] || {
  echo "A bidibip image already exists and will be permanently erased"
  read -p "Continue (y/N)?" choice
  case "$choice" in
    y|Y )
      bidibip_container_id=`sudo docker ps -a | grep bidibip | awk '{ print $1 }'`
      [ -z "$bidibip_container_id" ] || {
        echo -n 'stop container... '
        sudo docker stop $bidibip_container_id
        echo -n 'delete container... '
        sudo docker rm $bidibip_container_id
      }
      echo -n 'delete image... '
      sudo docker image rm $bidibip_image_id;;
    * ) return 0;;
  esac
}

echo -n 'create new image... '
sudo docker build /home/$USER/docker/Bidibip/ -t bidibip