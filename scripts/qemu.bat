echo Starting QEMU...
rem "C:\Program Files\qemu\qemu-system-i386.exe" os.img -soundhw ac97 -net nic,model=rtl8139 -serial file:test.txt -net tap,ifname=TAP -m 256 -D qemu.log
rem "C:\Program Files\qemu\qemu-system-i386.exe" -drive id=disk,file=..\..\os.img,if=none -device ahci,id=ahci -device ide-drive,drive=disk,bus=ahci.0  -soundhw ac97 -net nic,model=e1000 -serial file:serial.txtd  -m 256 -D qemu.log -net tap,ifname=TAP  -usbdevice tablet

rem -usbdevice disk:..\..\usbb.img
rem -drive if=none,id=usbstick,file=..\..\usbb.img -device usb-uhci,id=uhci -device usb-storage,bus=uhci.0,drive=usbstick

"C:\Program Files\qemu\qemu-system-i386.exe" ..\..\os.img -soundhw ac97 -net nic,model=e1000 -serial file:serial.txtd  -m 256 -D qemu.log  -net tap,ifname=TAP
rem net dump,file=netdump.wcap