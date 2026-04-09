
CREATE TABLE IF NOT EXISTS public.ai_pricing_policy
(
    id bigint NOT NULL DEFAULT nextval('ai_pricing_policy_id_seq'::regclass),
    provider character varying(32) COLLATE pg_catalog."default" NOT NULL,
    service character varying(32) COLLATE pg_catalog."default" NOT NULL,
    feature character varying(64) COLLATE pg_catalog."default" NOT NULL,
    meter character varying(32) COLLATE pg_catalog."default" NOT NULL,
    unit_price numeric(18,8) NOT NULL,
    currency character varying(8) COLLATE pg_catalog."default" NOT NULL DEFAULT 'USD'::character varying,
    credit_unit numeric(18,6),
    credit_name character varying(32) COLLATE pg_catalog."default",
    effective_from timestamp with time zone NOT NULL,
    effective_to timestamp with time zone NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
)

CREATE TABLE IF NOT EXISTS public.ai_usage_record
(
    id serial NOT NULL,
    user_id bigint,
    provider character varying(32) COLLATE pg_catalog."default" NOT NULL,
    service character varying(32) COLLATE pg_catalog."default" NOT NULL,
    feature character varying(64) COLLATE pg_catalog."default" NOT NULL,
    model character varying(64) COLLATE pg_catalog."default",
    meter character varying(32) COLLATE pg_catalog."default" NOT NULL,
    quantity numeric(18,6) NOT NULL,
    request_id integer,
    source character varying(32) COLLATE pg_catalog."default",
    type character varying(256) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    created_user_id integer NOT NULL,
)

CREATE TABLE public.users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(64) UNIQUE NOT NULL,
    email VARCHAR(256) UNIQUE NOT NULL,
    password_hash VARCHAR(256) NOT NULL,
    api_key VARCHAR(256) NOT NULL,
    status VARCHAR(32) NOT NULL DEFAULT 'active',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE public.ai_providers (
    id SERIAL PRIMARY KEY,
    deployment_name VARCHAR(64),
    api_endpoint VARCHAR(256) NOT NULL,
    api_key VARCHAR(256) NOT NULL,
    model_name VARCHAR(64),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_user_id INT NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_user_id INT NOT NULL
);

CREATE TABLE IF NOT EXISTS public.user_modes_config
(
    id serial NOT NULL,
    user_id bigint NOT NULL,
    ai_provider_id integer NOT NULL,
    status character varying(32) COLLATE pg_catalog."default" NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    modified_at timestamp with time zone NOT NULL DEFAULT now(),
    modified_user_id integer NOT NULL
)



