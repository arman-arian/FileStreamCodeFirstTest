namespace FileStreamCodeFirstTest
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class CreateTable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "Person.Images",
                c => new
                    {
                        Id = c.Guid(nullable: false),
                        PersonId = c.Guid(nullable: false),
                        Description = c.String(maxLength: 250),
                        ImageType = c.Short(nullable: false),
                        EndDate = c.Long(nullable: false),
                        CreatedTime = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.PersonId)
                .Index(t => new { t.PersonId, t.ImageType });
            
            Sql("ALTER DATABASE [DbFileStreamTest] ADD FILEGROUP [FG_PersonImages] contains FILESTREAM;", true);
            Sql("ALTER DATABASE [DbFileStreamTest] ADD FILE (NAME = 'FGS_PersonImages', FILENAME = 'D:\\PersonImages') TO FILEGROUP [FG_PersonImages];", true);
            Sql("ALTER TABLE [Person].[Images] ADD [FileId] UNIQUEIDENTIFIER ROWGUIDCOL NOT NULL;");
            Sql("ALTER TABLE [Person].[Images] ADD CONSTRAINT [UQ_Photos_FileId] UNIQUE NONCLUSTERED ([FileId]);");
            Sql("ALTER TABLE [Person].[Images] ADD CONSTRAINT [DF_Photos_FileId] DEFAULT (NEWSEQUENTIALID()) FOR [FileId];");
            Sql("ALTER TABLE [Person].[Images] ADD [Image] VARBINARY(MAX) FILESTREAM NOT NULL;");
            Sql("ALTER TABLE [Person].[Images] ADD CONSTRAINT [DF_Photos_Data] DEFAULT(0x) FOR [Image];");
            Sql(@"ALTER PROCEDURE [Person].[GetImageById] @Id uniqueidentifier AS
                  BEGIN
                  BEGIN TRANSACTION
                    SELECT TOP 1 [Id],[PersonId],[Description],[ImageType],[EndDate],[CreatedTime],[FileId],
			                   [Image].PathName() AS 'Path', GET_FILESTREAM_TRANSACTION_CONTEXT() AS 'FileStreamContext'
                    FROM [DbFileStreamTest].[Person].[Images]
                    WHERE Id = @Id
                  COMMIT TRANSACTION
                  END");
        }
        
        public override void Down()
        {
            Sql("ALTER TABLE [Person].[Images] DROP CONSTRAINT [DF_Photos_Data]");
            Sql("ALTER TABLE [Person].[Images] DROP COLUMN [Image]");
            Sql("ALTER TABLE [Person].[Images] DROP CONSTRAINT [UQ_Photos_FileId]");
            Sql("ALTER TABLE [Person].[Images] DROP CONSTRAINT [DF_Photos_FileId]");
            Sql("ALTER TABLE [Person].[Images] DROP COLUMN [FileId]");
            Sql("ALTER TABLE [Person].[Images] SET (FILESTREAM_ON=\"NULL\")");
            Sql("ALTER DATABASE [DbFileStreamTest] REMOVE FILE [FGS_PersonImages];", true);
            Sql("ALTER DATABASE [DbFileStreamTest] REMOVE FILEGROUP [FG_PersonImages];", true);
            Sql("DROP PROCEDURE [Person].[GetImageById]");

            DropIndex("Person.Images", new[] { "PersonId", "ImageType" });
            DropIndex("Person.Images", new[] { "PersonId" });
            DropTable("Person.Images");
        }
    }
}
