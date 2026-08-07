# Quick Setup

A step by step guide to setup the dev environment.

OS: Ubuntu 24.04.4 LTS

# step 1: install docker

Docker version: 5:29.7.1-1~ubuntu.24.04~noble

Run command `sudo bash .devcontainer/scripts/install/install_docker.sh` to install docker, add current user to the docker group and apply the change immediately.

Run `docker --version` to check if docker is installed.

Then verify that Docker is running with command
`sudo systemctl status docker`

If Docker is not running, start it manually:
`sudo systemctl start docker`

You can also run a hello-world container to verify installation with command

`sudo docker run hello-world`



## step 2: install devcontainers cli

Run command `sudo bash .devcontainer/scripts/install/install_devcontainer.sh` to install devcontainer.

Then run `source ~/.bashrc` to update the current terminal's $PATH and activate the devcontainer CLI.



## step 3: devcontainer cli usage

Once you have setup the vscode devcontainer extension, and you have installed the devcontainer cli, run `devcontainer --help` to view all commands.

Create and run dev container: `devcontainer up`

Create and run dev container after deleting existing contianer: `devcontainer up --remove-existing-container`

Then attach to the container from the workspace directory in vscode.

Alternatively, you can also use "Reopen in container" from vscode.
