# Mazuku

Mazuku is a web-based document editor with a fork & merge dynamic.

Any user can view a document, click _edit_ and save their own variant. Anyone can then merge any pair of documents if they want to combine them.

Unlike a Wiki, there is no single canonical version of any document. Instead, the various versions of each document form a family. Documents can therefore evolve apart if users wish them to do so.

Mazuku's views and editors use Markdown for styling.

## An Example

Each save stores a new document with its own unique identifier. The database keeps a record of the documents' geneology.

Suppose you create document X and save it. Document X now resides alone in the database.

If a user opens document X, makes a change and saves this change, it is saved as new document Y, and a record is kept showing that document X is the direct antecedent to document Y.

A user can then open documents X and Y together, choose the best parts of each and save. The new save becomes document Z, and a record is kept showing that documents X and Y both are direct antecedents to document Z.

## Installation

Mazuku is implemented in .NET Core with a solution file in this repository. It supports currently PostgreSQL as a backend, and therefore requires a working installation of PostgreSQL to run.

Installation instructions for a .NET Core server are beyond the scope of this document, however Mazuku is implemented as a by-the-book, "12 Factor" ASP.NET Core application and generic instructions apply.

Mazuku is configured using _environment variables_ instead of Microsoft's "appsettings.json" construct.

Let's examine the required and optional environment variables.

### POSTGRES_URL

This is an [Npgsql](http://www.npgsql.org) connection string. You will need to set up PostgreSQL with an empty database. Mazuku will populate it with tables at runtime.

### RECAPTCHA_SITEKEY, RECAPTCHA_SECRETKEY

These _optional_ environment variables are necessary to enable Google's ReCaptcha in the registration interface.

If you want to use ReCaptcha, you will need to set up an account to obtain these figures.

## Strategy

### Dependencies

Initially Mazuku was planned to depend on Amazon S3 for document storage and Auth0 for authentication, but these were eschewed in favor of an adhoc solution based on PostgreSQL. The reason for this is to avoid _institutional dependencies_.

A dependency on ReCaptcha, for example, is a dependency on Google as an institution. If the service were to be taken offline, or its APIs changed, the software would cease to function. A dependency on ReCaptcha, therefore, must be optional. Whereas ReCaptcha can be _removed_ from a live installation, ReCaptcha is allowed.

Conversely, PostgreSQL and .NET Core are open source projects which can be maintained and deployed by anyone.

### Caching

The long term goal of Mazuku's design is to leverage reverse proxy's caching abilities to reduce load on the database.

Documents, document metadata and account metadata and retrieved through API endpoints which return cache control headers and avoid change. Documents are immutable, though they cannot be cached permanently because it must be possible to redact them. Document metadata is immutable.

UI pages are constructed synchronously and with no calls to the database.

Authentication is done with JWT; session state is not kept in the database.

Over time, changes will be made to promote cacheability.
