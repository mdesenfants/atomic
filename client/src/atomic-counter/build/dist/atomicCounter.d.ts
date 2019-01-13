export interface ITenant {
    tenantName: string;
    origins: string[];
    writeKeys: string[];
    readKeys: string[];
}
export declare function increment(tenant: string, app: string, counter: string, key: string): Promise<void>;
export declare function count(tenant: string, app: string, counter: string, key: string): Promise<number>;
export declare function getAuthToken(token: string): Promise<string>;
export declare class AtomicCounterClient {
    private token;
    private tenants;
    constructor(authToken: string);
    createTenant(tenant: string): Promise<ITenant>;
    getTenant(tenant: string): Promise<ITenant>;
    increment(tenant: string, app: string, counter: string): Promise<void>;
    count(tenant: string, app: string, counter: string): Promise<number>;
}
