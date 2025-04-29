create database if not exists chat;
use chat;

create table if not exists user(
    user_id int auto_increment primary key,
    user_name varchar(100) not null,
    user_email varchar(100) not null unique,
    user_password varchar(256) not null,
    user_role int not null,
    user_created_at timestamp default current_timestamp,
    user_updated_at timestamp default current_timestamp on update current_timestamp
)engine = InnoDB default charset = utf8mb4 collate = utf8mb4_unicode_ci;

create table if not exists agent(
    agent_id int auto_increment primary key,
    agent_name varchar(100) not null,
    agent_description text,
    agent_config json,
    agent_status int,
    created_by_user int not null,
    agent_created_at timestamp default current_timestamp,
    agent_updated_at timestamp default current_timestamp on update current_timestamp,
    foreign key (created_by_user)
        references user (user_id)
        on delete set null
        on update cascade
)engine = InnoDB default charset = utf8mb4 collate = utf8mb4_unicode_ci;

create table if not exists permission(
    user_id int not null,
    agent_id int not null,
    permission_type int not null,
    primary key (user_id, agent_id),
    foreign key (user_id)
        references user (user_id)
        on delete cascade
        on update cascade,
    foreign key (agent_id)
        references agent (agent_id)
        on delete cascade
        on update cascade
)engine = InnoDB default charset = utf8mb4 collate = utf8mb4_unicode_ci;

create table if not exists conversation (
    conversation_id int auto_increment primary key,
    user_id int not null,
    agent_id int not null,
    started_at timestamp default current_timestamp,
    ended_at timestamp null,
    foreign key (user_id)
        references user (user_id)
        on delete cascade
        on update cascade,
    foreign key (agent_id)
        references agent (agent_id)
        on delete cascade
        on update cascade
)engine = InnoDB default charset = utf8mb4 collate = utf8mb4_unicode_ci;