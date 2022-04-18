#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

FROM mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2022
ARG source
WORKDIR /app

EXPOSE 9464 9465

COPY ${source:-bin/Debug/net471} .
ENTRYPOINT ["NetFrameworkExample.exe"]
