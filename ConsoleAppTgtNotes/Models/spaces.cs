//------------------------------------------------------------------------------
// <auto-generated>
//     Este código se generó a partir de una plantilla.
//
//     Los cambios manuales en este archivo pueden causar un comportamiento inesperado de la aplicación.
//     Los cambios manuales en este archivo se sobrescribirán si se regenera el código.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ConsoleAppTgtNotes.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class spaces
    {
        public int app_user_id { get; set; }
        public Nullable<int> capacity { get; set; }
        public string zip_code { get; set; }
    
        public virtual app app { get; set; }
    }
}
