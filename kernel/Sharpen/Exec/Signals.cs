﻿namespace Sharpen.Exec
{
    enum Signals
    {
        SIGHUP = 1,     // hangup
        SIGINT = 2,     // interrupt
        SIGQUIT = 3,    // quit
        SIGILL = 4,     // illegal instruction (not reset when caught)
        SIGTRAP = 5,    // trace trap (not reset when caught)
        SIGIOT = 6,     // IOT instruction
        SIGABRT = 6,    // used by abort, replace SIGIOT in the future
        SIGEMT = 7,     // EMT instruction
        SIGFPE = 8,     // floating point exception
        SIGKILL = 9,    // kill (cannot be caught or ignored)
        SIGBUS = 10,    // bus error
        SIGSEGV = 11,   // segmentation violation
        SIGSYS = 12,    // bad argument to system call
        SIGPIPE = 13,   // write on a pipe with no one to read it
        SIGALRM = 14,   // alarm clock
        SIGTERM = 15,   // software termination signal from kill
        SIGURG = 16,    // urgent condition on IO channel
        SIGSTOP = 17,   // sendable stop signal not from tty
        SIGTSTP = 18,   // stop signal from tty
        SIGCONT = 19,   // continue a stopped process
        SIGCHLD = 20,   // to parent on child stop or exit
        SIGCLD = 20,    // System V name for SIGCHLD
        SIGTTIN = 21,   // to readers pgrp upon background tty read
        SIGTTOU = 22,   // like TTIN for output if (tp->t_local&LTOSTOP)
        SIGIO = 23,     // input/output possible signal
        SIGXCPU = 24,   // exceeded CPU time limit
        SIGXFSZ = 25,   // exceeded file size limit
        SIGVTALRM = 26, // virtual time alarm
        SIGPROF = 27,   // profiling time alarm
        SIGWINCH = 28,  // window changed
        SIGLOST = 29,   // resource lost (eg, record-lock lost)
        SIGUSR1 = 30,   // user defined signal 1
        SIGUSR2 = 31,   // user defined signal 2
        NSIG = 32       // signal 0 implied
    }

    public unsafe class Signal
    {
        public int Number;
        public void* Function;
    }
}