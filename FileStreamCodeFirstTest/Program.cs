using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Infrastructure.Annotations;
using System.Data.Entity.Migrations;
using System.Data.Entity.ModelConfiguration;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace FileStreamCodeFirstTest
{
    class Program
    {
        public class Repository<TEntity, TContext>
            where TEntity : class
            where TContext : DbContext, new()
        {
            public TContext ActiveContext { get; set; }

            public Repository()
            {
                ActiveContext = new TContext();
            }

            public IList<TEntity> Get(Expression<Func<TEntity, bool>> expression)
            {

                var query = ActiveContext.Set<TEntity>().Where(expression).ToTraceString();
                if (string.IsNullOrEmpty(query))
                    return null;

                var fileStreamPropName = typeof(TEntity).GetProperties()
                    .Single(prop => Attribute.IsDefined(prop, typeof(FileStreamProp))).Name;

                var index = query.IndexOf("Select", StringComparison.Ordinal) + 7;
                query = query.Insert(index, string.Format(
                        "\r\n    [{0}].PathName() AS 'Path', \r\n    GET_FILESTREAM_TRANSACTION_CONTEXT() AS 'FileStreamContext',",
                        fileStreamPropName));

                var dbContextTransaction = ActiveContext.Database.BeginTransaction();

                var entities = ActiveContext.Database.SqlQuery<TEntity>(query).ToList();

                dbContextTransaction.Commit();
                dbContextTransaction.Dispose();

                foreach (var entity in entities)
                {
                    var path = (string) entity.GetType().GetProperties()
                        .Single(prop => Attribute.IsDefined(prop, typeof (FileStreamPath))).GetValue(entity);

                    var context = (byte[])entity.GetType().GetProperties()
                        .Single(prop => Attribute.IsDefined(prop, typeof(FileStreamContext))).GetValue(entity);

                    using (var source = new SqlFileStream(path, context, FileAccess.Read))
                    {
                        var buffer = new byte[16*1024];
                        using (var ms = new MemoryStream())
                        {
                            int bytesRead;
                            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                ms.Write(buffer, 0, bytesRead);
                            }
                            entity.GetType().GetProperties()
                                .Single(prop => Attribute.IsDefined(prop, typeof (FileStreamProp)))
                                .SetValue(entity, ms.ToArray());
                        }
                    }
                }

                return entities;
            }

            public int Update<T>(T entity)
            {
                throw new NotImplementedException();
            }

            public int Insert<T>(T entity)
            {
                throw new NotImplementedException();
            }
        }



        //public static PersonImage GetById(Guid id)
        //{
        //    using (var context = new TestContext())
        //    {
        //        using (var dbContextTransaction = context.Database.BeginTransaction())
        //        {
        //            var photo = context.PersonImages.FirstOrDefault(p => p.Id == id);
        //            if (photo == null)
        //                return null;

        //            var selectStatement = string.Format(RowDataStatement, context.GetTableName<PersonImage>());

        //            var rowData =
        //                context.Database.SqlQuery<FileStreamRowData>(selectStatement, new SqlParameter("id", id))
        //                    .First();

        //            using (var source = new SqlFileStream(rowData.Path, rowData.Transaction, FileAccess.Read))
        //            {
        //                var buffer = new byte[16*1024];
        //                using (var ms = new MemoryStream())
        //                {
        //                    int bytesRead;
        //                    while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
        //                    {
        //                        ms.Write(buffer, 0, bytesRead);
        //                    }
        //                    photo.Image = ms.ToArray();
        //                }
        //            }

        //            dbContextTransaction.Commit();

        //            return photo;
        //        }
        //    }
        //}

        public static void Update(PersonImage entity)
        {
            using (var context = new TestContext())
            {
                using (var tx = context.Database.BeginTransaction())
                {
                    try
                    {
                        context.Entry(entity).State = EntityState.Modified;
                        context.SaveChanges();
                        context.SavePhotoData(entity);
                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        public static void Insert(PersonImage entity)
        {
            using (var context = new TestContext())
            {
                using (var tx = context.Database.BeginTransaction())
                {
                    try
                    {
                        context.PersonImages.Add(entity);
                        context.SaveChanges();
                        context.SavePhotoData(entity);
                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        public static void Delete(Guid id)
        {
            using (var context = new TestContext())
            {
                context.Entry(new PersonImage { Id = id }).State = EntityState.Deleted;
                context.SaveChanges();
            }
        }

        //private static void SavePhotoData(DbContext context, PersonImage entity)
        //{
        //    var selectStatement = string.Format(RowDataStatement, context.GetTableName<PersonImage>());

        //    var rowData =
        //        context.Database.SqlQuery<FileStreamRowData>(selectStatement, new SqlParameter("id", entity.Id))
        //            .First();

        //    using (var destination = new SqlFileStream(rowData.Path, rowData.Transaction, FileAccess.Write))
        //    {
        //        var buffer = new byte[16 * 1024];
        //        using (var ms = new MemoryStream(entity.Image))
        //        {
        //            int bytesRead;
        //            while ((bytesRead = ms.Read(buffer, 0, buffer.Length)) > 0)
        //            {
        //                destination.Write(buffer, 0, bytesRead);
        //            }
        //        }
        //    }
        //}



        static void Main(string[] args)
        {
            //var photo = new PersonImage
            //{
            //    Id = Guid.NewGuid(),
            //    Description = "My Pic",
            //    CreatedTime = 102539,
            //    EndDate = 99999999,
            //    ImageType = PersonImageType.BusinessLicense,
            //    PersonId = Guid.NewGuid(),
            //    Image = ReadPhotoData(@"C:\Users\Arman-PC\Pictures\6.jpg")
            //};

            //Insert(photo);

            //var x = GetById(Guid.Parse("be81f744-5cda-46e5-8412-d2cf977943ba"));

            //TestContext ctx = new TestContext();
            //var z = ctx.PersonImages.Where(a => a.Id != Guid.Empty).ToString();

            var repo = new Repository<PersonImage, TestContext>();
            var x = repo.Get(a => a.Id == Guid.Empty).FirstOrDefault();

        }

        private static byte[] ReadPhotoData(string roomPhoto)
        {
            using (var source = File.OpenRead(roomPhoto))
            {
                var buffer = new byte[16 * 1024];
                using (var ms = new MemoryStream())
                {
                    int bytesRead;
                    while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                    }
                    return ms.ToArray();
                }
            }
        }


    }

    public static class Ex
    {
        public static void SavePhotoData(this DbContext context, PersonImage entity)
        {
            var rowDataStatement =
                @"SELECT Image.PathName() AS 'Path', GET_FILESTREAM_TRANSACTION_CONTEXT() AS 'Transaction' FROM {0} WHERE Id = @id";

            var selectStatement = string.Format(rowDataStatement, context.GetTableName<PersonImage>());

            var rowData =
                context.Database.SqlQuery<FileStreamRowData>(selectStatement, new SqlParameter("id", entity.Id))
                    .First();

            using (var destination = new SqlFileStream(rowData.Path, rowData.Transaction, FileAccess.Write))
            {
                var buffer = new byte[16*1024];
                using (var ms = new MemoryStream(entity.Image))
                {
                    int bytesRead;
                    while ((bytesRead = ms.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        destination.Write(buffer, 0, bytesRead);
                    }
                }
            }
        }
    }


    public class FileStreamRowData
    {
        public string Path { get; set; }

        public byte[] Transaction { get; set; }
    }

    public class TestContext : DbContext
    {
        public TestContext() : base("Data Source=.;Initial Catalog=DbFileStreamTest;Integrated Security=True")
        {
            Database.SetInitializer(
                new MigrateDatabaseToLatestVersion<TestContext, TestConfiguration>());

            this.Configuration.LazyLoadingEnabled = false;
            this.Configuration.AutoDetectChangesEnabled = false;
            this.Configuration.ProxyCreationEnabled = false;
            this.Configuration.ValidateOnSaveEnabled = false;
        }

        public virtual DbSet<PersonImage> PersonImages { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Configurations.Add(new PersonImageEntityTypeConfiguration());
        }
    }

    internal sealed class TestConfiguration : DbMigrationsConfiguration<TestContext>
    {
        public TestConfiguration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = true;
        }

        protected override void Seed(TestContext context)
        {
        }
    }

    public static class DbContextExtensions
    {
        public static string GetTableName<T>(this DbContext context) where T : class
        {
            var workspace = ((IObjectContextAdapter)context).ObjectContext.MetadataWorkspace;
            var mappingItemCollection = (StorageMappingItemCollection)workspace.GetItemCollection(DataSpace.CSSpace);
            var storeContainer = ((EntityContainerMapping)mappingItemCollection[0]).StoreEntityContainer;
            var baseEntitySet = storeContainer.BaseEntitySets.Single(es => es.Name == typeof(T).Name);
            return string.Format("{0}.{1}", baseEntitySet.Schema, baseEntitySet.Table);
        }

        public static List<string> GetIgnoredColumns(this DbContext context, string entityTypeName)
        {
            return null;
        }
    }

    /// <summary>مدرک شناسایی شخص</summary>
    public class PersonImage
    {
        public Guid Id { get; set; }

        /// <summary>شناسه</summary>
        public Guid PersonId { get; set; }

        /// <summary>تصویر </summary>
        [FileStreamProp]
        public byte[] Image { get; set; }

        [FileStreamPath]
        public string Path { get; set; }

        [FileStreamContext]
        public byte[] FileStreamContext { get; set; }

        /// <summary>توضیحات</summary>
        public string Description { get; set; }

        /// <summary>نوع مدرک شناسایی</summary>
        public PersonImageType ImageType { get; set; }

        /// <summary>تاریخ پایان اعتبار</summary>
        public long EndDate { get; set; }

        /// <summary>زمان ایجاد</summary>
        public int CreatedTime { get; set; }
    }



    public class FileStreamPath : NotMappedAttribute
    {
    }

    public class FileStreamContext : NotMappedAttribute
    {
    }

    public class FileStreamProp : NotMappedAttribute
    {
    }

    public enum PersonImageType : short
    {
        [Description("تصویر شخص")] PersonAvatar = 1,
        [Description("شناسنامه")] Crety,
        [Description("توضیحات شناسنامه")] CretyNote,
        [Description("کارت ملی")] NationalCard,
        [Description("تصویر اثر انگشت")] FingerprintImage,
        [Description("تصویر فیش حقوقی")] PayRolImage,
        [Description("جواز کسب")] BusinessLicense,
        [Description("گواهی کسر از حقوق")] CertifiedPayrollDeduction,
        [Description("کارت اعتباری")] CreditCard,
        [Description("قبوض شهری")] MunicipalBill,
        [Description("گذرنامه")] Passport,
        [Description("ویزا")] Visa,
        [Description("پروانه اقامت")] ResidencePermit,
        [Description("آگهی تاسیس")] FoundedAd,
        [Description("اظهار نامه/اساسنامه")] Declaration,
        [Description("گواهی نامه ثبت شرکت")] CompanyRegistrationCertificate,
        [Description("پروانه وکالت")] LawyersLicense,
        [Description("جواز نظام پزشکی")] MedicalSystemLicense,
        [Description("سایر")] Other = 9999
    }

    public class PersonImageEntityTypeConfiguration : EntityTypeConfiguration<PersonImage>
    {
        public PersonImageEntityTypeConfiguration()
        {
            this.ToTable("Images", "Person");
            this.HasKey(p => p.Id);
            this.Property(p => p.Description).HasMaxLength(250);
            this.Property(p => p.PersonId)
                .HasColumnAnnotation(IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute("IX_PersonId"),
                        new IndexAttribute("IX_PersonId_ImageType", 1)
                    }));
            this.Property(p => p.ImageType)
                .HasColumnAnnotation(IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new IndexAttribute("IX_PersonId_ImageType", 2)));
            
            //this.Ignore(p => p.Image); // This ensures that Entity Framework ignores the "Data" column when mapping
            //this.Ignore(p => p.Path);
            //this.Ignore(p => p.FileStreamContext);
        }
    }

    public static class IQueryableExtensions
    {
        /// <summary>
        /// For an Entity Framework IQueryable, returns the SQL with inlined Parameters.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <returns></returns>
        public static string ToTraceQuery<T>(this IQueryable<T> query)
        {
            var objectQuery = GetQueryFromQueryable(query);
            if (objectQuery == null) return string.Empty;
            var result = objectQuery.ToTraceString();
            foreach (var parameter in objectQuery.Parameters)
            {
                var name = "@" + parameter.Name;
                var value = "'" + parameter.Value + "'";
                result = result.Replace(name, value);
            }
            return result;
        }

        public static string ToTraceString<T>(this IQueryable<T> query)
        {
            var objectQuery = GetQueryFromQueryable(query);
            var traceString = new StringBuilder();
            traceString.AppendLine(objectQuery.ToTraceString());
            traceString.AppendLine();
            foreach (var parameter in objectQuery.Parameters)
            {
                traceString.AppendLine(parameter.Name + " [" + parameter.ParameterType.FullName + "] = " + parameter.Value);
            }
            return traceString.ToString();
        }

        private static ObjectQuery<T> GetQueryFromQueryable<T>(IQueryable<T> query)
        {
            var internalQueryField = query.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(f => f.Name.Equals("_internalQuery"));
            if (internalQueryField == null) return null;
            var internalQuery = internalQueryField.GetValue(query);
            var objectQueryField = internalQuery.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(f => f.Name.Equals("_objectQuery"));
            if (objectQueryField != null)
                return objectQueryField.GetValue(internalQuery) as ObjectQuery<T>;
            return null;
        }
    }
}
