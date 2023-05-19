#!/bin/sh

sudo cp -r /var/lib/docker/volumes/bidibip-saved/_data/ /home/ubuntu/docker/Bidibip/saved/
mv /home/ubuntu/docker/Bidibip/saved/_data/* /home/ubuntu/docker/Bidibip/saved
rmdir /home/ubuntu/docker/Bidibip/saved/_data
