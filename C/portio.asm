; Sharpen_Arch_PortIO_Out8(ushort port, byte value)
global Sharpen_Arch_PortIO_Out8
Sharpen_Arch_PortIO_Out8:
    mov dx, [esp + 1 * 4]
    mov al, [esp + 2 * 4]
    out dx, al
    ret

; Sharpen_Arch_PortIO_In8(ushort port)
global Sharpen_Arch_PortIO_In8
Sharpen_Arch_PortIO_In8:
    mov dx, [esp + 1 * 4]
    in byte al, dx
    ret

; Sharpen_Arch_PortIO_Out16(ushort port, ushort value)
global Sharpen_Arch_PortIO_Out16
Sharpen_Arch_PortIO_Out16:
    mov dx, [esp + 1 * 4]
    mov ax, [esp + 2 * 4]
    out dx, ax
    ret

; Sharpen_Arch_PortIO_In16(ushort port)
global Sharpen_Arch_PortIO_In16
Sharpen_Arch_PortIO_In16:
    mov dx, [esp + 1 * 4]
    in word ax, dx
    ret

; Sharpen_Arch_PortIO_Out32(ushort port, uint value)
global Sharpen_Arch_PortIO_Out32
Sharpen_Arch_PortIO_Out32:
    mov dx, [esp + 1 * 4]
    mov eax, [esp + 2 * 4]
    out dx, eax
    ret

; Sharpen_Arch_PortIO_In32(ushort port)
global Sharpen_Arch_PortIO_In32
Sharpen_Arch_PortIO_In32:
    mov dx, [esp + 1 * 4]
    in dword eax, dx
    ret