﻿//------------------------------------------------------------------------------
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
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class TgtNotesEntities : DbContext
    {
        public TgtNotesEntities()
            : base("name=TgtNotesEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<activity> activity { get; set; }
        public virtual DbSet<admin> admin { get; set; }
        public virtual DbSet<app> app { get; set; }
        public virtual DbSet<artist_genres> artist_genres { get; set; }
        public virtual DbSet<artists> artists { get; set; }
        public virtual DbSet<chats> chats { get; set; }
        public virtual DbSet<contracts> contracts { get; set; }
        public virtual DbSet<files> files { get; set; }
        public virtual DbSet<genres> genres { get; set; }
        public virtual DbSet<incidences> incidences { get; set; }
        public virtual DbSet<languages> languages { get; set; }
        public virtual DbSet<matches> matches { get; set; }
        public virtual DbSet<messages> messages { get; set; }
        public virtual DbSet<notifications> notifications { get; set; }
        public virtual DbSet<rating> rating { get; set; }
        public virtual DbSet<roles> roles { get; set; }
        public virtual DbSet<spaces> spaces { get; set; }
        public virtual DbSet<temp_match> temp_match { get; set; }
    }
}
