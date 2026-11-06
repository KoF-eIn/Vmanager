@echo off
set GIT="C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe"
%GIT% checkout develop
%GIT% add .
%GIT% commit -m "docs: Add internship practice report and embedded DB scripts"
%GIT% checkout main
%GIT% merge develop
%GIT% log --oneline -n 12
