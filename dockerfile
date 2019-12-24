FROM jenkins/jenkins:lts
 # Switch to root to install .NET Core SDK
USER root

# Just for my sanity... Show me this distro information!
RUN uname -a && cat /etc/*release

# Based on instructiions at https://docs.microsoft.com/en-us/dotnet/core/linux-prerequisites?tabs=netcore2x
# Install depency for dotnet core 3

# Install the .Net Core framework, set the path, and show the version of core installed.
RUN apt-get update
RUN apt-get install apt-transport-https
RUN wget -q https://packages.microsoft.com/config/ubuntu/19.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb

RUN dpkg -i packages-microsoft-prod.deb
RUN apt-get update -y
RUN apt-get install dotnet-sdk-3.1 -y

RUN export PATH=$PATH:$HOME/dotnet
RUN    dotnet --version

# Install Chrome
RUN wget https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb
RUN dpkg -i google-chrome-stable_current_amd64.deb; apt-get -fy install

# Good idea to switch back to the jenkins user.
USER jenkins