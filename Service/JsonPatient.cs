﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Service
{
    public class JsonPatient
    {
        public String RequestID
        {
            get; set;
        }

        public String PatientID
        {
            get; set;
        }
        public bool Insurance
        {
            get; set;
        }
    }
}
