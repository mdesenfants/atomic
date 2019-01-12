export interface ITenant {
    tenantName: string;
    origins: string[];
    writeKeys: string[];
    readKeys: string[];
}
export declare function increment(key: string): Promise<void>;
export declare function count(key: string): Promise<number>;
export declare function getAuthToken(token: string): Promise<string>;
export declare class AtomicCounterClient {
    private token;
    private tenants;
    constructor(authToken: string);
    createTenant(): Promise<ITenant>;
    getTenant(): Promise<ITenant>;
    increment(): Promise<void>;
    count(): Promise<number>;
}
