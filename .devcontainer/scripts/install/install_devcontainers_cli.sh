#!/bin/bash

curl -fsSL https://raw.githubusercontent.com/devcontainers/cli/main/scripts/install.sh | sh

# update .bashrc for all future terminal sessions
echo 'export PATH="$HOME/.devcontainers/bin:$PATH"' >> ~/.bashrc

# remember to source .bashrc after this to load the change
# or you could execute the file in the current shell by `source filename`
