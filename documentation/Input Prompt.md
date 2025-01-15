```
Input: EstateKit - Personal Information API (business logic and data access)
WHY - Vision & Purpose
Purpose & Users
We need a GraphQL API that will enable callers to:
Allow calling applications to get the personal information for a specific user id that is passed to it. 
This API will contain any business logic required for the personal information, such as decrypting or encrypting any fields that are sensitive data. 
This API will not be contacting the database directly. 
Front facing applications will use this API, both web and mobile applications.
This API will be part of a larger set of APIs that make up the Estate Kit application.
We also need a second REST-based API that will receive personal information from the first, business logic API, and then save it to the database. 
This API will be the only application that contacts the EstateKit database for any CRUD methods. 
This API will be heavily monitored for any malicious use, unusually large numbers of requests, and unusual activity. 
All interactions with this API will need to be confirmed with the central OAuth security provider before proceeding with the processing of data. 
Any data field that has an attribute of “Sensitive data” will need to be encrypted by calling the Estate Kit encryption API, passing it the user id and piece of data to encrypt. 

What - Core Requirements
	Both the business logic and data APIs will be reading, creating, updating or deleting the following data fields:
Single Field information
Full Legal Name - Separate fields for first name, last name, middle names, if any. 
Maiden name - if applicable
Nicknames - an array of alternate names
Date of birth - Sensitive data
Place of birth - address entry will consist of line 1 and 2, city, province/state and country
Marital status
Vehicle key/access information
Religious affiliation
Military service, country and branch
Education
Occupation
Employer
Citizenship information


Lists
Phone numbers
Home, work, Cellular

Government Identifiers 
Passport ID number - Sensitive data
Provincial ID - Sensitive data
Social Insurance Number - Sensitive data
Taxpayer Identification number - Sensitive data
Driver’s license number and expiry date - Sensitive data - Driver’s license document
Trusted Traveler Program ID(E.g., Global Entry, Nexus, SENTRI) - Sensitive data
Work id - Sensitive data

            Addresses
Usual vehicle location
PO box number and location
Father’s birthplace
Mother’s birthplace
Location of safety deposit box/safe/storage locker, access information
Burial place of parents and close relatives

Contacts
Parents - include maiden names if relevant
Siblings
marital/common law partner
Former spouses
Children
Grandchildren
Close relatives

Important documents - 
birth certificate
Citizenship documentation/visas
Marriage certificate/cohabitation agreement
Separation agreement
Divorce certificate/divorce order/settlement docs
Orders that relate to children, property or support
Prenuptial agreement
Adoption papers
Military ID / discharge papers
calendar / appointment book
Address book
PO box key
Record of usernames and passwords
Spare keys

Business Logic API:
The business logic API will provide methods to read, create, update and delete personal information of the user id provided. 

	Reading the personal information data: 
The business logic APIwill receive a user id parameter that it will call the data API with, in order to receive the personal information for the user. 
The system will then return the data back to the downstream system. 

	Creating or updating the personal information data: 
The business logic API will receive some or all of the data to be created or updated, validate the data for processing, and then pass the data to the data API to save to the EstateKit database.
For documents, the system must save any uploaded documents into a secure S3 bucket in AWS, and set the path in the user_document.location field, before passing it to the data API.
For government IDs fields, the API must provide the ability for the user to be able to upload a photo of the front and a photo of the back of the government issued document (i.e. driver’s license). The system should then process the photos through an OCR reader (AWS Textract), that will provide the identifier and expiration date data to be saved in the user_identifiers table.


Data API
	Reading the personal information data: 
When a request is made for getting the personal information for a user, the system will retrieve the raw data by default, meaning that no data will be decrypted. 
Only if a parameter is passed to it that indicates that a correct passKey has been provided for the user data should the system then decrypt the data. The system will decrypt the data by calling the Estate Kit decryption service, passing the user_id with the data to be decrypted. 
	
Creating or updating the personal information data: 
The data API will receive data from the business logic API. It will then check the attributes of each data field and determine which data fields are sensitive data.
For each data field that is sensitive data, the system must contact the Estate Kit Encryption API, passing it the user id and data to be encrypted. 
The encryption API will return the encrypted data back to the data API, which will save the data to the database. 


HOW - Planning & Implementation
Technical Foundation
Required Stack Components
Business Logic API
Backend: GraphQL API service for business logic. Use .net Core 9 with C#
Storage: AWS S3 bucket

Data API
Backend: REST API service for database access.  Use .net Core 9 with C#
ORM: Entity Framework 10
Personal data storage database: existing Postgres RDBMS - estatekit DB

Overall system
OCR Engine: AWS Textract
PaaS: AWS
Container orchestration: AWS EKS
Authentication: AWS Cognito (OAuth)

System Requirements
Performance: load time under 3 seconds
Security: End-to-end encryption, secure authentication, financial regulatory compliance. 
Reliability: 99.9% uptime, daily data synchronization, automated backups
Testing: Comprehensive unit testing, security testing
Business Requirements
All calls to the business logic or data APIs must contain valid security tokens from the OAuth provider. 
There should be no calls allowed that are not using OAuth authentication 
Architectural Notes
Three databases will be in use 
Personal information - estatekit 
Security information - eksecurity
User encryption/decryption keys - ekvault
The system must provide a GraphQL interface for all API endpoints for the business logic APIs
The business APIs will call the data API service, which will be in a separate VPC from the business APIs. 
The only service that can communicate with the databases will be the data API service. 


Data Structure

Data tables
access_info
id bigint NOT NULL DEFAULT nextval('access_info_id_seq'::regclass),
key_location_id bigint NOT NULL,
access_code character varying(50) COLLATE pg_catalog."default",
access_instructions character varying(20000) COLLATE pg_catalog."default",
active boolean NOT NULL DEFAULT true,
username character varying(500) COLLATE pg_catalog."default",
password character varying(1000) COLLATE pg_catalog."default",

company
id bigint NOT NULL DEFAULT nextval('company_id_seq'::regclass),
name character varying(300) COLLATE pg_catalog."default" NOT NULL,
address_id bigint,
active boolean NOT NULL DEFAULT true,

contact 
id bigint NOT NULL DEFAULT nextval('contact_id_seq'::regclass),
first_name character varying(20) COLLATE pg_catalog."default" NOT NULL,
last_name character varying(20) COLLATE pg_catalog."default" NOT NULL,
middle_name character(20) COLLATE pg_catalog."default",
maiden_name character varying(20) COLLATE pg_catalog."default",
active boolean DEFAULT true,

Contact_address
 id bigint NOT NULL DEFAULT nextval('contact_address_id_seq'::regclass),
 contact_id bigint NOT NULL,
 address_id bigint NOT NULL,
 address_type_id bigint NOT NULL,
 is_default boolean NOT NULL DEFAULT false,
 active boolean DEFAULT true,

Contact_citizenship
id bigint NOT NULL DEFAULT nextval('contact_citizenship_id_seq'::regclass),
contact_id bigint NOT NULL,
citizen_type_id bigint NOT NULL,
country_id bigint NOT NULL,
start_date date,
end_date date,
active boolean NOT NULL DEFAULT true,

Contact_company
id bigint NOT NULL DEFAULT nextval('contact_company_id_seq'::regclass),
contact_id bigint NOT NULL,
company_id bigint NOT NULL,
occupation character varying(300) COLLATE pg_catalog."default",
start_date date,
end_date date,
active boolean NOT NULL DEFAULT true,

Contact_relationship
id bigint NOT NULL DEFAULT nextval('contact_relationship_id_seq'::regclass),
contact_id bigint NOT NULL,
related_contact_id bigint NOT NULL,
relationship_type_id bigint NOT NULL,
active boolean NOT NULL DEFAULT true,


Country
id bigint NOT NULL DEFAULT nextval('country_id_seq'::regclass),
name character varying(20) COLLATE pg_catalog."default" NOT NULL,
country_code character varying(5) COLLATE pg_catalog."default" NOT NULL,
active boolean NOT NULL DEFAULT true,
CONSTRAINT country_pkey PRIMARY KEY (id)

Contact_contact_method
id bigint NOT NULL DEFAULT nextval('contact_contact_method_id_seq'::regclass),
contact_id bigint NOT NULL,
contact_method_type_id bigint NOT NULL,
contact_value character varying(50) COLLATE pg_catalog."default" NOT NULL,
is_default boolean NOT NULL DEFAULT false,
active boolean NOT NULL DEFAULT true,



Document_identifier_map
id bigint NOT NULL DEFAULT nextval('document_identifier_map_id_seq'::regclass),
field_name character varying(200) COLLATE pg_catalog."default" NOT NULL,
identifier_type_id bigint,
user_document_type_id bigint,
active boolean NOT NULL DEFAULT true,


Prov_state
id bigint NOT NULL DEFAULT nextval('prov_state_id_seq'::regclass),
country_id bigint NOT NULL DEFAULT nextval('prov_state_country_id_seq'::regclass),
name character varying(20) COLLATE pg_catalog."default" NOT NULL,
code character varying(5) COLLATE pg_catalog."default" NOT NULL,
active boolean NOT NULL DEFAULT true,

Religious_denomination
id bigint NOT NULL DEFAULT nextval('religious_denomination_id_seq'::regclass),
religion_type_id bigint NOT NULL,
name character varying(20) COLLATE pg_catalog."default" NOT NULL,
active boolean DEFAULT true,


Types
id bigint NOT NULL DEFAULT nextval('types_id_seq'::regclass),
type_group_id bigint NOT NULL DEFAULT nextval('types_type_group_id_seq'::regclass),
key character varying(10) COLLATE pg_catalog."default",
name character varying(10) COLLATE pg_catalog."default" NOT NULL,
active boolean NOT NULL DEFAULT true,

Type_group
id bigint NOT NULL DEFAULT nextval('type_group_id_seq'::regclass),
name character varying(100) COLLATE pg_catalog."default" NOT NULL,
key character varying(100) COLLATE pg_catalog."default" NOT NULL,
active boolean NOT NULL DEFAULT true,

User
id bigint NOT NULL DEFAULT nextval('user_id_seq'::regclass),
contact_id bigint NOT NULL,
known_as character varying(100) COLLATE pg_catalog."default",
date_of_birth date,
birth_address_id bigint,
key_access_info_id bigint,
active boolean NOT NULL DEFAULT true,
marital_status_type_id bigint,

User_asset
id bigint NOT NULL DEFAULT nextval('user_asset_id_seq'::regclass),
user_id bigint NOT NULL,
access_info_id bigint,
name character varying COLLATE pg_catalog."default",
asset_type_id bigint NOT NULL,
location_address_id bigint,
active boolean NOT NULL DEFAULT true,
asset_id bigint,
asset_name character varying(500) COLLATE pg_catalog."default",


User_civil_service
id bigint NOT NULL DEFAULT nextval('user_civil_service_id_seq'::regclass),
user_id bigint NOT NULL,
service_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
start_date date,
end_date date,
active boolean DEFAULT true,
branch_name character varying COLLATE pg_catalog."default",
country_id bigint,


User_denomination
id bigint NOT NULL DEFAULT nextval('user_denomination_id_seq'::regclass),
user_id bigint NOT NULL,
denomination_id bigint NOT NULL,
start_date date,
end_date date,
location_address_id bigint,
active boolean NOT NULL DEFAULT true,

User_document
id bigint NOT NULL DEFAULT nextval('user_document_id_seq'::regclass),
user_id bigint NOT NULL,
document_type_id bigint NOT NULL,
digital_front_photo_location text COLLATE pg_catalog."default",
digital_back_photo_location text COLLATE pg_catalog."default",
relevant boolean NOT NULL DEFAULT true,
location text COLLATE pg_catalog."default",
in_kit boolean NOT NULL DEFAULT false,
active boolean NOT NULL DEFAULT true,


Vehicle_asset
id bigint NOT NULL DEFAULT nextval('vehicle_id_seq'::regclass),
make character varying(200) COLLATE pg_catalog."default" NOT NULL,
model character varying(200) COLLATE pg_catalog."default" NOT NULL,
year smallint NOT NULL,
colour character varying(200) COLLATE pg_catalog."default",
"VIN" character varying(200) COLLATE pg_catalog."default",
active boolean NOT NULL DEFAULT true,

Data Relationships

Single field data
Full Legal Name : user.contact_id -> contact.first_name, user.contact_id -> contact.middle_name, user.contact_id -> contact.last_name
Maiden name : user.contact_id -> contact.maiden_name
Nicknames : user.known_as
Date of birth : user.date_of_birth
Place of birth : user.birth_address_id -> Address.line1, address.line2, address.city, address.prov_state_id -> prov_state.name, address.prov_state_id -> prov_state.country_id -> country.name, address.postal_zip
Marital status : user.marital_status_type_id -> Type.name
Vehicle key/access information : user->user_asset (asset_type_id -> types.id : type.key == ‘VEHICLE’) .access_info_id -> access_info.key_location_id -> address
Religious affiliation: user -> user_denomination -> religious_denomination.name + religious_denomination.religion_type_id -> Type.name
Military service, country and branch: user -> user_civil_service.service_name, user_service.branch_name, user_service.country > country.name
Education: user.contact_id -> contact_education.education_facility_name
Occupation: user -> contact -> contact_company.occupation
Employer: user -> contact -> contact_company -> company.name
Citizenship information: user.contact_id ->contact_citizenship.citizen_type_id -> types.name, user.contact_id ->contact_citizenship.country_id -> country.name, contact_citizenship.start_date, contact_citizenship.end_date

Lists
Phone numbers
Work Phone: user -> contact -> contact_contact_method.(contact_method_type_id -> types join type_group on type_group.id = types.type_group_id where  types.code = ‘WORK_PHONE’  and type_group.key = ‘CONTACT_METHOD_TYPE’)
HomePhone: user -> contact -> contact_contact_method.(contact_method_type_id -> types join type_group on type_group.id = types.type_group_id where  types.code = ‘HOME_PHONE’  and type_group.key = ‘CONTACT_METHOD_TYPE’)
Cell: user -> contact -> contact_contact_method.(contact_method_type_id -> types join type_group on type_group.id = types.type_group_id where  types.code = ‘CELL_PHONE’  and type_group.key = ‘CONTACT_METHOD_TYPE’)


Addresses
Usual vehicle location: User -> contact_address.address_type_id = types.code = ‘VEHICLE_LOCATION’ and type_group_id.code = ‘COMMON_ADDRESS_TYPES’
PO box number and location: User -> contact_address.address_type_id = types.code = ‘PO BOX NUMBER’ and type_group_id.code = ‘COMMON_ADDRESS_TYPES’
Father’s birthplace: User -> contact_address.address_type_id = types.code = ‘FATHER_BIRTHPLACE’ and type_group_id.code = ‘COMMON_ADDRESS_TYPES’
Mother’s birthplace: User -> contact_address.address_type_id = types.code = ‘MOTHER_BIRTHPLACE’ and type_group_id.code = ‘COMMON_ADDRESS_TYPES’
Location of safety deposit box/safe/storage locker, access information : user->user_asset (asset_type_id -> types.id : type.key == ‘OTHER’) .access_info_id -> access_info.key_location_id -> address
Burial place of parents and close relatives: User -> contact_address.address_type_id = types.code = ‘FAMILY_BURIAL_LOCATION’ and type_group_id.code = ‘COMMON_ADDRESS_TYPES’

Contacts
Mother: user -> contact_relationship -> contact.first_name, contact.last_name, contact.maden_name where contact_relationship.relationship_type_id = type.’MOTHER’ and type.type_group_id.code = ‘CONTACT_RELATIONSHIP_TYPES’
Father: user -> contact_relationship -> contact.first_name, contact.last_name, contact.maden_name where contact_relationship.relationship_type_id = type.’FATHER’ and type.type_group_id.code = ‘CONTACT_RELATIONSHIP_TYPES’
Sister: user -> contact_relationship -> contact.first_name, contact.last_name, contact.maden_name where contact_relationship.relationship_type_id = type.’SISTER’ and type.type_group_id.code = ‘CONTACT_RELATIONSHIP_TYPES’
Brother: user -> contact_relationship -> contact.first_name, contact.last_name, contact.maiden_name where contact_relationship.relationship_type_id = type.’BROTHER’ and type.type_group_id.code = ‘CONTACT_RELATIONSHIP_TYPES’
marital/common law partner: user -> contact_relationship -> contact.first_name, contact.last_name, contact.maden_name where contact_relationship.relationship_type_id = type.’COMMON_LAW_SPOUSE’ and type.type_group_id.code = ‘CONTACT_RELATIONSHIP_TYPES’
Former spouses: user -> contact_relationship -> contact.first_name, contact.last_name, contact.maden_name where contact_relationship.relationship_type_id = type.’FORMER_SPOUSE’ and type.type_group_id.code = ‘CONTACT_RELATIONSHIP_TYPES’
Daughter: user -> contact_relationship -> contact.first_name, contact.last_name, contact.maden_name where contact_relationship.relationship_type_id = type.’DAUGHTER’ and type.type_group_id.code = ‘CONTACT_RELATIONSHIP_TYPES’
Son: user -> contact_relationship -> contact.first_name, contact.last_name, contact.maden_name where contact_relationship.relationship_type_id = type.’SON; and type.type_group_id.code = ‘CONTACT_RELATIONSHIP_TYPES’
GrandDaughter: user -> contact_relationship -> contact.first_name, contact.last_name, contact.maden_name where contact_relationship.relationship_type_id = type.’GRANDDAUGHTER’ and type.type_group_id.code = ‘CONTACT_RELATIONSHIP_TYPES’
Grandson: user -> contact_relationship -> contact.first_name, contact.last_name, contact.maden_name where contact_relationship.relationship_type_id = type.’GRANDSON’ and type.type_group_id.code = ‘CONTACT_RELATIONSHIP_TYPES’
Close relatives: user -> contact_relationship -> contact.first_name, contact.last_name, contact.maden_name where contact_relationship.relationship_type_id = type.’RELATIVE’ and type.type_group_id.code = ‘CONTACT_RELATIONSHIP_TYPES’

Government Identifiers
Driver’s license Photo: user_document join types on document_type_id = types.id where types.code = DRIVERS_LICENSE and type_group.CODE = ‘USER_DOCUMENT’
Driver’s license ID: user -> User_identifiers -> Types where types.code = ‘DRIVERS_LICENSE_NUMBER’ and type_group.code = ‘IDENTIFIER_TYPES’

Provincial ID Photo: user_document join types on document_type_id = types.id where
types.code = PROVINCIAL_ID and type_group.CODE = ‘USER_DOCUMENT’
Provincial ID: user -> User_identifiers -> Types where types.code = ‘PROVINCIAL_ID’ and type_group.code = ‘IDENTIFIER_TYPES’
Taxpayer ID Photo: user_document join types on document_type_id = types.id where types.code = TAXPAYER_ID and type_group.CODE = ‘USER_DOCUMENT’
TaxpayerID: user -> User_identifiers -> Types where types.code = ‘TAXPAYER_ID’ and type_group.code = ‘IDENTIFIER_TYPES’

Social Insurance Card Photo: user_document join types on document_type_id = types.id where types.code = SOCIAL_INSURANCE_NUMBER and type_group.CODE = ‘USER_DOCUMENT’
Social Insurance Number: user -> User_identifiers -> Types where types.code = ‘SOCIAL_INSURANCE_NUMBER’ and type_group.code = ‘IDENTIFIER_TYPES’

Passport Photo: user_document join types on document_type_id = types.id where types.code = PASSPORT and type_group.CODE = ‘USER_DOCUMENT’
Passport ID: user -> User_identifiers -> Types where types.code = ‘PASSORT_ID’ and type_group.code = ‘IDENTIFIER_TYPES’

Trusted Traveler Program Card photo (E.g., Global Entry, Nexus, SENTRI): user_document join types on document_type_id = types.id where types.code = TRUSTED_TRAVELLER and type_group.CODE = ‘USER_DOCUMENT’
Trusted Traveler Program ID(E.g., Global Entry, Nexus, SENTRI): user -> User_identifiers -> Types where types.code = ‘TRUSTED_TRAVELLER_ID’ and type_group.code = ‘IDENTIFIER_TYPES’

Work id photo: user_document join types on document_type_id = types.id where types.code = WORK_ID and type_group.CODE = ‘USER_DOCUMENT’
Work id: user -> User_identifiers -> Types where types.code = ‘WORK_ID’ and type_group.code = ‘IDENTIFIER_TYPES’


Important documents - Location and if they’re in the kit
birth certificate: user -> user_document join types on document_type_id = types.id where types.code = ‘BIRTH_CERTIFICATE’ and type_group.CODE = ‘USER_DOCUMENT’
Citizenship documentation/visas: user_document join types on document_type_id = types.id where types.code = CITIZENSHIP_DOCUMENTATION and type_group.CODE = ‘USER_DOCUMENT’
Marriage certificate/cohabitation agreement: user_document join types on document_type_id = types.id where types.code = MARRIAGE_CERTIFICATE and type_group.CODE = ‘USER_DOCUMENT’
Separation agreement: user_document join types on document_type_id = types.id where types.code = SEPARATION_AGREEMENT and type_group.CODE = ‘USER_DOCUMENT’
Divorce certificate/divorce order/settlement docs: user_document join types on document_type_id = types.id where types.code = DIVORCE_CERTIFICATE and type_group.CODE = ‘USER_DOCUMENT’
Orders that relate to children, property or support: user_document join types on document_type_id = types.id where types.code = CHILD_CUSTODY_DOCUMENT and type_group.CODE = ‘USER_DOCUMENT’
Prenuptial agreement: user_document join types on document_type_id = types.id where types.code = PRENUPTUAL_AGREEMENT and type_group.CODE = ‘USER_DOCUMENT’
Adoption papers: user_document join types on document_type_id = types.id where types.code = ADOPTION_PAPERS and type_group.CODE = ‘USER_DOCUMENT’
calendar / appointment book: user_document join types on document_type_id = types.id where types.code = CALENDAR_APPOINTMENT_BOOK and type_group.CODE = ‘USER_DOCUMENT’
Address book: user_document join types on document_type_id = types.id where types.code = ADDRESS_BOOK  and type_group.CODE = ‘USER_DOCUMENT’
Record of usernames and passwords: user_document join types on document_type_id = types.id where types.code = USERNAMES_DOCUMENT and type_group.CODE = ‘USER_DOCUMENT’
Military ID / discharge papers: user_document join types on document_type_id = types.id where types.code = MILITARY_ID and type_group.CODE = ‘USER_DOCUMENT’
```