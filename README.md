Window Service Installation Steps:

1-Get Latest the Project after that you have to build that Window Serive Project.
2-Check there is .exe bin/Debug folder of that Project.
3-Go to that Localtion "C:\Windows\Microsoft.NET\Framework\v4.0.30319" open a CMD as admin.
4-Run this Commad 'installutil -i "Path_to_Your_WindowService_EXE"'.
5-After that you need to check your service is install for that press Windows + R.and Type 'services.msc' hit Enter.Check there is your service avalible if yes Start the service.
6.You can test the Service my Creating a TXT file with the name of TemTest.txt.If the File open after creating than service is working properly now.


Kindly Restore the DB BackUp For Sample Testing.