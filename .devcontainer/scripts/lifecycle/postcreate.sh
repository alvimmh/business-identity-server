#!/bin/bash

script_file_path=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)

bash $script_file_path/../install/install_dotnet_tools.sh
