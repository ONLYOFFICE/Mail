PUSHD %~dp0..
dotnet build ASC.Mail.sln --configuration Release /fl1 /flp1:LogFile=build/ASC.Mail.log;Verbosity=Normal
