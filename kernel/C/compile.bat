@echo off
call ..\..\scripts\pre

echo Assembling..
nasm %KERNEL_ASM_FLAGS% arch\arch.asm -o build\arch.o
nasm %KERNEL_ASM_FLAGS% arch\int19.asm -o build\int19.o

echo Compiling..
i686-elf-gcc %KERNEL_C_FLAGS% kernel.c -c -o build\kernel.o

echo Generating symbols...
i686-elf-ld %KERNEL_LD_FLAGS% build\*.o -o kernel
i686-elf-nm kernel -g > symbols.txt
%DUMP_SYMBOLS% symbols.txt symbols.asm
nasm %KERNEL_ASM_FLAGS% symbols.asm -o build\symbols.o

echo Linking kernel...
i686-elf-ld %KERNEL_LD_FLAGS% -s build\*.o -o kernel

echo Copying kernel...
call ..\..\scripts\mount
copy kernel G:\kernel
call ..\..\scripts\unmount

call ..\..\scripts\qemu.bat