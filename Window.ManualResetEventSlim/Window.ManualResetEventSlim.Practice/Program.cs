﻿using System;
using System.Threading.Tasks;
using System.Threading;

namespace Window.ManualResetEventSlim.Practice
{
    class Program
    {
        static void Main()
        {
            MRES_SetWaitReset();
            MRES_SpinCountWaitHandle();
        }
        // Demonstrates:
        //      ManualResetEventSlim construction
        //      ManualResetEventSlim.Wait()
        //      ManualResetEventSlim.Set()
        //      ManualResetEventSlim.Reset()
        //      ManualResetEventSlim.IsSet
        static void MRES_SetWaitReset()
        {
            System.Threading.ManualResetEventSlim mres1 = new System.Threading.ManualResetEventSlim(); // initialize as unsignaled

            // Start an asynchronous Task that manipulates mres3 and mres2
            var observer = Task.Factory.StartNew(() =>
            {
                mres1.Wait();
                Console.WriteLine("mres1=" + mres1.IsSet);
                Console.WriteLine("observer sees signaled mres1!");
                Console.WriteLine("observer resetting mres3...");
                Console.WriteLine("observer signalling mres2");
            });

            Console.WriteLine("first");
            Console.WriteLine("second");
            mres1.Set(); //ture This will "kick off" the observer Task 
            //mres2.Wait(); // This won't return until observer Task has finished resetting mres3
            Console.WriteLine("main thread sees signaled mres2!");
            Console.WriteLine("main thread: mres3.IsSet = {0} (should be false)");

            // It's good form to Dispose() a ManualResetEventSlim when you're done with it
            observer.Wait(); // make sure that this has fully completed
            mres1.Dispose();
            Console.ReadLine();
        }

        // Demonstrates:
        //      ManualResetEventSlim construction w/ SpinCount
        //      ManualResetEventSlim.WaitHandle
        static void MRES_SpinCountWaitHandle()
        {
            // Construct a ManualResetEventSlim with a SpinCount of 1000
            // Higher spincount => longer time the MRES will spin-wait before taking lock
            System.Threading.ManualResetEventSlim mres1 = new System.Threading.ManualResetEventSlim(false, 1000);
            System.Threading.ManualResetEventSlim mres2 = new System.Threading.ManualResetEventSlim(false, 1000);

            Task bgTask = Task.Factory.StartNew(() =>
            {
                // Just wait a little
                Thread.Sleep(100);

                // Now signal both MRESes
                Console.WriteLine("Task signalling both MRESes");
                mres1.Set();
                mres2.Set();
            });

            // A common use of MRES.WaitHandle is to use MRES as a participant in 
            // WaitHandle.WaitAll/WaitAny.  Note that accessing MRES.WaitHandle will
            // result in the unconditional inflation of the underlying ManualResetEvent.
            WaitHandle.WaitAll(new WaitHandle[] { mres1.WaitHandle, mres2.WaitHandle });
            Console.WriteLine("WaitHandle.WaitAll(mres1.WaitHandle, mres2.WaitHandle) completed.");

            // Clean up
            bgTask.Wait();
            mres1.Dispose();
            mres2.Dispose();
        }
    }
}