docker kill netframeworkexample
docker rm netframeworkexample
docker rmi netframeworkexample
dotnet build
docker build -t netframeworkexample .
docker run -d --name netframeworkexample --isolation=process -p 9464:9464 -p 9465:9465 netframeworkexample
