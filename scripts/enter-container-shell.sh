#!/bin/sh

container_id=`sudo docker ps -a | grep bidibip | awk '{ print $1 }'`

[ -z "$container_id" ] && {
  echo 'there is no available container of bidibip'
  return 0
}

sudo docker exec -it $container_id /bin/sh