﻿using Sharpen.Collections;
using Sharpen.Drivers.Power;

namespace Sharpen.Arch
{
    class IOApicManager
    {
        private static List m_IOApics;

        /// <summary>
        /// Initializes the IO Apic Manager
        /// </summary>
        public static void Init()
        {
            m_IOApics = new List();
        }

        /// <summary>
        /// Adds a new IO Apic
        /// </summary>
        /// <param name="IOApic">The IO Apic</param>
        public static void Add(IOApic IOApic)
        {
            m_IOApics.Add(IOApic);
        }

        /// <summary>
        /// Initializes the found IO Apics
        /// </summary>
        public static void InitIOApics()
        {
            for (int i = 0; i < m_IOApics.Count; i++)
            {
                IOApic IOApic = (IOApic)m_IOApics.Item[i];
                IOApic.Init();
            }
        }

        /// <summary>
        /// Get the IO Apic responsible for the interrupt source
        /// </summary>
        /// <param name="src">Interrupt source</param>
        /// <returns>The IO Apic</returns>
        public static IOApic GetIOApicFor(uint src)
        {
            for (int i = 0; i < m_IOApics.Count; i++)
            {
                IOApic IOApic = (IOApic)m_IOApics.Item[i];
                if (src >= IOApic.GlobalSystemInterruptBase && src < IOApic.GlobalSystemInterruptBase + IOApic.RedirectionCount)
                {
                    return IOApic;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates an IRQ redirection entry
        /// </summary>
        /// <param name="pin">The IO Apic pin</param>
        /// <param name="interrupt">The interrupt vector associated with the pin</param>
        /// <param name="flags">Extra flags</param>
        public static void CreateEntry(uint pin, uint interrupt, ulong flags)
        {
            IOApic IOApic = GetIOApicFor(pin);
            if (IOApic == null)
            {
                Console.Write("[IOAPIC] No IO Apic was found responsible for pin ");
                Console.WriteNum((int)pin);
                Console.Write('\n');
                return;
            }

            IOApic.CreateRedirection(pin - IOApic.GlobalSystemInterruptBase, 32 + interrupt, flags);
        }

        /// <summary>
        /// Creates an ISA IRQ redirection entry
        /// </summary>
        /// <param name="src">The source interrupt</param>
        /// <param name="dst">The destination interrupt</param>
        public static void CreateISARedirection(uint src, uint dst)
        {
            Acpi.ISAOverride irq = Acpi.GetISARedirection(src);

            // Polarity and Trigger
            ulong flags = 0;
            if (irq.Polarity == 0 || irq.Polarity == 3)
                flags |= IOApic.IOAPIC_REDIR_POLARITY_LOW;
            else
                flags |= IOApic.IOAPIC_REDIR_POLARITY_HIGH;

            if (irq.Trigger == 0 || irq.Trigger == 1)
                flags |= IOApic.IOAPIC_REDIR_TRIGGER_EDGE;
            else
                flags |= IOApic.IOAPIC_REDIR_TRIGGER_LEVEL;

            // No override?
            uint irqNum = (irq.GSI == 0) ? src : dst;

            IOApic responsible = GetIOApicFor(irqNum);
            if (responsible == null)
            {
                Console.Write("[IOAPIC] No IO Apic was found responsible for pin ");
                Console.WriteNum((int)src);
                Console.Write("->");
                Console.WriteNum((int)dst);
                Console.Write('\n');
                return;
            }

            responsible.CreateRedirection(irqNum - responsible.GlobalSystemInterruptBase, 32 + dst, flags);
        }
    }
}
