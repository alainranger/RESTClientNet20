docker build -t restclientnet20.fakewebapi .\RESTClientNet20.FakeWebApi

docker run -d `
    --name restclientnet20.fakewebapi `
    -p 5000:8080 `
    restclientnet20.fakewebapi

try {
    & "C:\Program Files\NUnit 2.6.4\bin\nunit-console.exe" `
      LegacyRestClient.Tests.dll
}
finally {
    docker stop restclientnet20.fakewebapi
    docker rm restclientnet20.fakewebapi
}