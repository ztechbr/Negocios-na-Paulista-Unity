@echo off
setlocal
rem Paulista Business World - atalhos de execucao
rem   run.bat            executa o jogo (gera o build se ainda nao existir)
rem   run.bat build      gera o build Windows e executa
rem   run.bat webgl      gera o build WebGL e abre no navegador (http://localhost:8000)
rem   run.bat editor     abre o projeto no Unity Editor
rem   run.bat full       executa em tela cheia
rem Defina UNITY_EXE para usar outra instalacao do Unity.

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
set "GAME=%ROOT%\Builds\Windows\PaulistaBusinessWorld.exe"
if not defined UNITY_EXE set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe"

if /i "%~1"=="build"  goto build
if /i "%~1"=="webgl"  goto webgl
if /i "%~1"=="editor" goto editor
if /i "%~1"=="full"   goto full

if not exist "%GAME%" goto build
goto play

:build
call :check_unity || exit /b 1
echo Gerando build Windows (alguns minutos)...
"%UNITY_EXE%" -batchmode -nographics -projectPath "%ROOT%" -executeMethod ProjectSetup.BuildWindows -logFile "%ROOT%\Logs\build-windows.log"
if errorlevel 1 (
    echo Falha no build. Veja "%ROOT%\Logs\build-windows.log".
    pause
    exit /b 1
)
goto play

:play
echo Iniciando Paulista Business World...
start "" "%GAME%" -screen-fullscreen 0 -screen-width 1600 -screen-height 900
exit /b 0

:full
if not exist "%GAME%" (
    call :check_unity || exit /b 1
    "%UNITY_EXE%" -batchmode -nographics -projectPath "%ROOT%" -executeMethod ProjectSetup.BuildWindows -logFile "%ROOT%\Logs\build-windows.log" || exit /b 1
)
start "" "%GAME%" -screen-fullscreen 1
exit /b 0

:webgl
call :check_unity || exit /b 1
echo Gerando build WebGL (alguns minutos)...
"%UNITY_EXE%" -batchmode -nographics -projectPath "%ROOT%" -executeMethod ProjectSetup.BuildWebGL -logFile "%ROOT%\Logs\build-webgl.log"
if errorlevel 1 (
    echo Falha no build. Veja "%ROOT%\Logs\build-webgl.log".
    pause
    exit /b 1
)
where python >nul 2>nul
if errorlevel 1 (
    echo Python nao encontrado. Sirva a pasta "%ROOT%\Builds\WebGL" com qualquer servidor HTTP.
    pause
    exit /b 1
)
echo Abrindo http://localhost:8000 - feche esta janela para parar o servidor.
start "" http://localhost:8000
python -m http.server 8000 --directory "%ROOT%\Builds\WebGL"
exit /b 0

:editor
call :check_unity || exit /b 1
start "" "%UNITY_EXE%" -projectPath "%ROOT%"
exit /b 0

:check_unity
if exist "%UNITY_EXE%" exit /b 0
echo Unity nao encontrado em "%UNITY_EXE%".
echo Instale o Unity 6000.6.5f1 pelo Unity Hub ou defina a variavel UNITY_EXE.
pause
exit /b 1
