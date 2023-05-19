#!/bin/sh

mkdir -p /home/ubuntu/docker/Bidibip/saved/
sudo cp -r /var/lib/docker/volumes/bidibip-saved/_data/ /home/ubuntu/docker/Bidibip/saved/
sudo mv /home/ubuntu/docker/Bidibip/saved/_data/* /home/ubuntu/docker/Bidibip/saved
sudo rmdir /home/ubuntu/docker/Bidibip/saved/_data