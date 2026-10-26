#!/bin/bash

# Get the container id
container_id=$(docker container ls -a -f name=main_db -q)

# Stop the container
docker container stop "$container_id"

# Remove the container
docker container rm "$container_id"
