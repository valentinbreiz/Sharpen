sudo qemu-system-i386 ../../os.img -soundhw ac97 -device pcnet,netdev=network0,mac=52:55:00:d1:55:01 -netdev tap,id=network0,ifname=tap0,script=no,downscript=no -m 256 -D qemu.log
