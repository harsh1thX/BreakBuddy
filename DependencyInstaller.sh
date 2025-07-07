#!/bin/bash

set -e  # Exit on any error

echo "=== Dependency Installer Script ==="
echo "Installing Go, pipx, and security toolkit..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

print_status() {
    echo -e "${GREEN}[Here We Go]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Detect package manager
detect_package_manager() {
    if command -v apt-get &> /dev/null; then
        echo "apt"
    elif command -v yum &> /dev/null; then
        echo "yum"
    elif command -v dnf &> /dev/null; then
        echo "dnf"
    elif command -v pacman &> /dev/null; then
        echo "pacman"
    elif command -v zypper &> /dev/null; then
        echo "zypper"
    elif command -v apk &> /dev/null; then
        echo "apk"
    else
        echo "unknown"
    fi
}

# Install packages based on package manager
install_packages() {
    local pkg_manager=$1
    shift
    local packages=("$@")
    
    case $pkg_manager in
        "apt")
            sudo apt-get update
            sudo apt-get --assume-yes install "${packages[@]}"
            ;;
        "yum")
            sudo yum update -y
            sudo yum install -y "${packages[@]}"
            ;;
        "dnf")
            sudo dnf update -y
            sudo dnf install -y "${packages[@]}"
            ;;
        "pacman")
            sudo pacman -Syu --noconfirm
            sudo pacman -S --noconfirm "${packages[@]}"
            ;;
        "zypper")
            sudo zypper refresh
            sudo zypper install -y "${packages[@]}"
            ;;
        "apk")
            sudo apk update
            sudo apk add "${packages[@]}"
            ;;
        *)
            print_error "Unsupported package manager. Please install packages manually:"
            printf '%s\n' "${packages[@]}"
            exit 1
            ;;
    esac
}

# Detect package manager
PKG_MANAGER=$(detect_package_manager)
print_status "Detected package manager: $PKG_MANAGER"

# Define packages for different package managers
declare -A PACKAGES
PACKAGES[apt]="git make gcc python3-pip python3-venv python3-full libpcap-dev wget ruby ruby-dev ruby-rubygems golang-go pipx"
PACKAGES[yum]="git make gcc python3-pip python3-setuptools libpcap-devel wget ruby ruby-devel rubygems golang python3-pipx"
PACKAGES[dnf]="git make gcc python3-pip python3-setuptools libpcap-devel wget ruby ruby-devel rubygems golang python3-pipx"
PACKAGES[pacman]="git make gcc python-pip python-setuptools libpcap wget ruby rubygems go python-pipx"
PACKAGES[zypper]="git make gcc python3-pip python3-setuptools libpcap-devel wget ruby ruby-devel rubygems go python3-pipx"
PACKAGES[apk]="git make gcc python3 py3-pip libpcap-dev wget ruby ruby-dev ruby-gems go py3-pipx"

# Install system packages
print_status "Installing system packages..."
if [[ -n "${PACKAGES[$PKG_MANAGER]}" ]]; then
    install_packages $PKG_MANAGER ${PACKAGES[$PKG_MANAGER]}
else
    print_error "No package list defined for $PKG_MANAGER"
    exit 1
fi

# Install distribution-specific security tools
install_security_tools() {
    print_status "Installing wpscan via gem..."
    if command -v gem &> /dev/null; then
        sudo gem install wpscan
        print_status "wpscan installed successfully"
    else
        print_error "Ruby gems not available, cannot install wpscan"
    fi
}

print_status "Installing security tools..."
install_security_tools

# Install Go
print_status "Installing Go..."
if ! command -v go &> /dev/null; then
    # Check if Go was installed via package manager
    export PATH=$PATH:/usr/local/go/bin
    export PATH=$PATH:$(go env GOPATH 2>/dev/null)/bin 2>/dev/null || true
    
    if command -v go &> /dev/null; then
        print_status "Go installed via package manager: $(go version)"
    else
        # Fallback to manual installation
        GO_VERSION="1.21.5"
        GO_TARBALL="go${GO_VERSION}.linux-amd64.tar.gz"
        
        print_status "Downloading Go ${GO_VERSION}..."
        wget -q "https://golang.org/dl/${GO_TARBALL}" -O "/tmp/${GO_TARBALL}"
        
        print_status "Installing Go to /usr/local..."
        sudo rm -rf /usr/local/go
        sudo tar -C /usr/local -xzf "/tmp/${GO_TARBALL}"
        
        export PATH=$PATH:/usr/local/go/bin
        export PATH=$PATH:$(go env GOPATH)/bin
        
        print_status "Go installed manually: $(go version)"
    fi
    
    # Add Go to PATH if not already present
    if ! grep -q "/usr/local/go/bin" ~/.bashrc; then
        echo 'export PATH=$PATH:/usr/local/go/bin' >> ~/.bashrc
        echo 'export PATH=$PATH:$(go env GOPATH)/bin' >> ~/.bashrc
    fi
else
    print_status "Go is already installed: $(go version)"
fi

# Install pipx
print_status "Setting up pipx..."
if command -v pipx &> /dev/null; then
    python3 -m pipx ensurepath
    export PATH="$HOME/.local/bin:$PATH"
    print_status "pipx is ready to use"
else
    print_error "pipx installation failed"
fi

# Install Go tools
print_status "Installing Go-based security tools..."
go install -v github.com/projectdiscovery/subfinder/v2/cmd/subfinder@latest
go install -v github.com/PentestPad/subzy@latest
CGO_ENABLED=1 go install github.com/projectdiscovery/katana/cmd/katana@latest
go install github.com/lc/gau/v2/cmd/gau@latest 
go install github.com/rverton/gxss@latest
go install github.com/Emoe/kxss@latest
go install github.com/hahwul/dalfox/v2@latest
go install -v github.com/projectdiscovery/httpx/cmd/httpx@latest
go install -v github.com/projectdiscovery/nuclei/v3/cmd/nuclei@latest
go install github.com/KathanP19/Gxss@latest
go install github.com/tomnomnom/qsreplace@latest
go install github.com/ffuf/ffuf/v2@latest
go install -v github.com/projectdiscovery/naabu/v2/cmd/naabu@latest
go install github.com/tomnomnom/assetfinder@latest
go install github.com/tomnomnom/httprobe@latest
go install -v github.com/sa7mon/s3scanner@latest

# Install masscan
print_status "Installing masscan..."
if ! command -v masscan &> /dev/null; then
    cd /tmp
    git clone https://github.com/robertdavidgraham/masscan
    cd masscan
    make
    sudo make install
    cd ..
    rm -rf masscan
    print_status "masscan installed successfully"
else
    print_status "masscan is already installed"
fi

# Install Python tools
print_status "Installing Python-based security tools..."
pipx install arjun
pipx install uro

# Handle corscanner installation with fallback options
print_status "Installing corscanner..."
if pipx install corscanner; then
    print_status "corscanner installed via pipx"
elif pip3 install --user corscanner; then
    print_status "corscanner installed via pip --user"
elif pip3 install --break-system-packages corscanner; then
    print_warning "corscanner installed with --break-system-packages flag"
else
    print_error "Failed to install corscanner"
fi

print_status "Installation completed successfully!"
print_status "Please run 'source ~/.bashrc' or restart your terminal to update PATH"

echo -e "\n${GREEN}=== Installation Summary ===${NC}"
echo "✓ Golang for Tools Compilation"
echo "✓ pipx package manager"
echo "✓ Security toolkit tools"
echo "✓ All dependencies installed"