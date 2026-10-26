#!/bin/bash

# Added ownership permission handling so the non-root vscode user can
# read/write to the mounted volumes without permission conflicts.
sudo chown -R vscode:vscode /home/vscode/.gemini /home/vscode/.config 2>/dev/null || true

script_file_path=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)

bash $script_file_path/../install/install_dotnet_tools.sh

bash $script_file_path/../install/restore_packages.sh

bash $script_file_path/../install/initialize_databases.sh
